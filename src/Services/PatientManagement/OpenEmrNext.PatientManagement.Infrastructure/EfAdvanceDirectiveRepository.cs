using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;

namespace OpenEmrNext.PatientManagement.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IAdvanceDirectiveRepository"/>. Raw SQL throughout against legacy's
/// own <c>documents</c>, <c>categories_to_documents</c>, and <c>patient_data</c> tables (ADR 0049 -- no
/// new table), the same style <see cref="EfInsurancePolicyRepository"/> and
/// <see cref="EfDuplicateScorer"/> already use for tables (or columns) outside the EF-mapped
/// <c>Patient</c> entity.
/// </summary>
public sealed class EfAdvanceDirectiveRepository(PatientManagementDbContext db, IAdvanceDirectiveFileStore fileStore)
    : IAdvanceDirectiveRepository
{
    // Seeded by every fresh legacy install (sql/database.sql: id 6, codes 'LOINC:42348-3') and the same
    // category advancedirectives.php's own document browser looks up by this exact name
    // ("SELECT id FROM categories WHERE name='Advance Directive'"). Looked up by name, not hardcoded as
    // an id, so a install that renumbered it still tags (and later finds) documents correctly.
    private const string AdvanceDirectiveCategoryName = "Advance Directive";

    public async Task AddDocumentAsync(AdvanceDirectiveDocument document, CancellationToken ct)
    {
        var url = await fileStore.SaveAsync(document.Pid, document.DriveUuid, document.Content, ct);

        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(document.Uuid);
        var driveUuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(document.DriveUuid);

        // Same LAST_INSERT_ID() connection-pinning concern as EfInsurancePolicyRepository.AddAsync --
        // here it also has to pin across the id-generation statement below, which depends on it too.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // documents.id is NOT an AUTO_INCREMENT column in legacy -- it's allocated from a single global
        // counter (the `sequences` table, one row, shared by many legacy tables) via
        // QueryUtils::generateId() / ADOdb's GenID(). We have to draw from that exact same counter, not
        // our own, so a ever-still-running legacy writer never collides with an id we hand out.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sequences SET id = LAST_INSERT_ID(id + 1)", ct);
        var id = await db.Database.SqlQueryRaw<long>("SELECT LAST_INSERT_ID() AS Value").SingleAsync(ct);
        document.AssignGeneratedIdentity(id);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO documents
                (id, uuid, type, size, date, url, mimetype, owner, revision, foreign_id, docdate, hash,
                 name, drive_uuid, storagemethod, path_depth, encounter_id)
            VALUES
                ({id}, {uuidBytes}, 'file_url', {document.Size}, NOW(), {url}, {document.MimeType},
                 {document.OwnerId}, NOW(), {document.Pid}, {document.EffectiveDate?.ToString("yyyy-MM-dd")}, {document.Sha3Hash},
                 {document.Name}, {driveUuidBytes}, 0, 1, 0)
            """, ct);

        var categoryId = await db.Database.SqlQueryRaw<long>(
            "SELECT id AS Value FROM categories WHERE name = {0}", AdvanceDirectiveCategoryName)
            .SingleOrDefaultAsync(ct);

        if (categoryId != 0)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"REPLACE INTO categories_to_documents (category_id, document_id) VALUES ({categoryId}, {id})", ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<AdvanceDirectiveDocumentSummary>> GetDocumentsAsync(long pid, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<RawDocumentRow>(
            """
            SELECT d.id AS Id, d.uuid AS Uuid, d.name AS Name, d.mimetype AS MimeType, d.size AS Size,
                   CAST(d.docdate AS CHAR(10)) AS DocDate, d.owner AS Owner
            FROM documents d
            JOIN categories_to_documents c2d ON c2d.document_id = d.id
            JOIN categories c ON c.id = c2d.category_id
            WHERE d.foreign_id = {0} AND d.deleted = 0 AND c.name = {1}
            """, pid, AdvanceDirectiveCategoryName).ToListAsync(ct);

        return [.. rows.Select(r => new AdvanceDirectiveDocumentSummary(
            r.Id,
            Rfc4122GuidConverter.FromRfc4122Bytes(r.Uuid),
            AdvanceDirectiveTypeExtensions.ClassifyFromName(r.Name),
            r.Name ?? "",
            r.MimeType ?? "",
            r.Size,
            TryParseDateOnly(r.DocDate),
            r.Owner))];
    }

    public async Task<(string MimeType, string FileName, byte[] Content)?> GetDocumentContentAsync(long pid, Guid uuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(uuid);

        var row = await db.Database.SqlQueryRaw<RawDocumentLocation>(
            "SELECT url AS Url, mimetype AS MimeType, name AS Name FROM documents WHERE uuid = {0} AND foreign_id = {1} AND deleted = 0",
            uuidBytes, pid).SingleOrDefaultAsync(ct);

        if (row is null)
            return null;

        var content = await fileStore.ReadAsync(row.Url ?? "", ct);
        return (row.MimeType ?? "", row.Name ?? "", content);
    }

    public async Task SetStatusAsync(
        long pid, AdvanceDirectiveCompletionStatus status, DateTime? reviewedAtUtc,
        long? authenticatorUserId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE patient_data
            SET completed_ad = {status.ToStoredValue()},
                ad_reviewed = {reviewedAtUtc},
                advance_directive_user_authenticator = {authenticatorUserId}
            WHERE pid = {pid}
            """, ct);
    }

    public async Task<(AdvanceDirectiveCompletionStatus Status, DateTime? ReviewedAtUtc, long? AuthenticatorUserId)> GetStatusAsync(
        long pid, CancellationToken ct)
    {
        var row = await db.Database.SqlQueryRaw<RawStatusRow>(
            "SELECT completed_ad AS CompletedAd, ad_reviewed AS AdReviewed, advance_directive_user_authenticator AS AuthenticatorId FROM patient_data WHERE pid = {0}",
            pid).SingleOrDefaultAsync(ct);

        return row is null
            ? (AdvanceDirectiveCompletionStatus.Unknown, null, null)
            : (AdvanceDirectiveCompletionStatusExtensions.FromStoredValue(row.CompletedAd), row.AdReviewed, row.AuthenticatorId);
    }

    private static DateOnly? TryParseDateOnly(string? stored) =>
        DateOnly.TryParseExact(stored, "yyyy-MM-dd", out var day) ? day : null;

    private sealed class RawDocumentRow
    {
        public long Id { get; set; }
        public byte[] Uuid { get; set; } = [];
        public string? Name { get; set; }
        public string? MimeType { get; set; }
        public int Size { get; set; }
        public string? DocDate { get; set; }
        public long? Owner { get; set; }
    }

    private sealed class RawDocumentLocation
    {
        public string? Url { get; set; }
        public string? MimeType { get; set; }
        public string? Name { get; set; }
    }

    private sealed class RawStatusRow
    {
        public string? CompletedAd { get; set; }
        public DateTime? AdReviewed { get; set; }
        public long? AuthenticatorId { get; set; }
    }
}
