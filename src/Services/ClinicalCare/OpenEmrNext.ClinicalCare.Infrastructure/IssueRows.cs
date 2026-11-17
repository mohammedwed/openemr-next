using Microsoft.EntityFrameworkCore;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The parts of writing an issue that are the same whatever its type, in one place (ADR 0070 #3, LEG-2): the <c>uuid_registry</c> row legacy's
/// <c>UuidRegistry::createUuid</c> inserts for every uuid it hands out, and the <c>lists_touch</c> marker the issue screen writes. Each aggregate's own
/// registry owns its <c>lists</c> INSERT, because the columns differ. Both statements must run inside the caller's transaction.
/// </summary>
internal static class IssueRows
{
    /// <summary>Registers the uuid so legacy recognises it as its own (it backfills and validates uuids through this table).</summary>
    public static Task RegisterUuidAsync(ClinicalCareDbContext db, byte[] uuidBytes, CancellationToken ct, string tableName = "lists") =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO uuid_registry (uuid, table_name, table_id, table_vertical, couchdb, document_drive, mapped, created)
            VALUES ({uuidBytes}, {tableName}, 'id', '', '', 0, 0, NOW())
            """, ct);

    /// <summary>
    /// Marks the patient's list of this type as addressed, as <c>setListTouch</c> does: legacy's summary reads it as <c>listTouched</c>. <c>(pid, type)</c> is
    /// the primary key, so <c>INSERT IGNORE</c> is race-free and leaves a row legacy wrote first exactly as it was.
    /// </summary>
    public static Task TouchAsync(ClinicalCareDbContext db, long pid, string type, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT IGNORE INTO lists_touch (pid, type, `date`) VALUES ({pid}, {type}, NOW())", ct);
}
