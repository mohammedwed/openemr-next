using System.Security.Cryptography;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;

namespace OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;

/// <summary>
/// One uploaded advance-directive file, modeled as a legacy <c>documents</c> row (ADR 0049 -- no new
/// table). <see cref="Id"/> is database-generated, same deferred-identity pattern as
/// <c>InsurancePolicy</c>, except here the id comes from legacy's own shared <c>sequences</c> counter
/// (<c>QueryUtils::generateId()</c>), not <c>AUTO_INCREMENT</c> -- legacy's <c>documents.id</c> isn't an
/// identity column at all.
/// </summary>
/// <remarks>
/// <see cref="Name"/> deliberately prefixes the original filename with the type's own display text
/// (<c>"Living Will - consent.pdf"</c>) so that legacy's own <c>determineAdvanceDirectiveType()</c> --
/// which classifies purely by matching keywords in the document name, there being no dedicated type
/// column -- always classifies a document this service writes correctly, including when read back by
/// legacy's own PHP code, not just this service's.
/// </remarks>
public sealed class AdvanceDirectiveDocument
{
    public long Id { get; private set; }
    public Guid Uuid { get; }
    public Guid DriveUuid { get; }
    public long Pid { get; }
    public AdvanceDirectiveType Type { get; }
    public string Name { get; }
    public string MimeType { get; } = null!;
    public byte[] Content { get; } = null!;
    public int Size { get; }
    public string Sha3Hash { get; } = null!;
    public DateOnly? EffectiveDate { get; }
    public long OwnerId { get; }

    private AdvanceDirectiveDocument(
        long pid, AdvanceDirectiveType type, string originalFileName, string mimeType,
        byte[] content, DateOnly? effectiveDate, long ownerId)
    {
        Pid = pid;
        Type = type;
        Name = $"{type.ToDisplayName()} - {originalFileName}";
        MimeType = mimeType;
        Content = content;
        Size = content.Length;
        Sha3Hash = Convert.ToHexStringLower(SHA3_512.HashData(content));
        EffectiveDate = effectiveDate;
        OwnerId = ownerId;
        Uuid = Guid.NewGuid();
        DriveUuid = Guid.NewGuid();
    }

    /// <exception cref="AdvanceDirectiveDocumentValidationException">One or more fields fail validation.</exception>
    public static AdvanceDirectiveDocument Upload(
        long pid, AdvanceDirectiveType type, string originalFileName, string mimeType,
        byte[] content, DateOnly? effectiveDate, long ownerId)
    {
        var errors = new List<string>();

        if (pid <= 0)
            errors.Add("Pid must identify an existing patient.");

        if (ownerId <= 0)
            errors.Add("OwnerId must identify the uploading user.");

        if (originalFileName.Length is 0 or > 255)
            errors.Add("File name must be between 1 and 255 characters.");

        if (string.IsNullOrWhiteSpace(mimeType))
            errors.Add("MimeType is required.");

        if (content.Length == 0)
            errors.Add("Content must not be empty.");

        if (errors.Count > 0)
            throw new AdvanceDirectiveDocumentValidationException(errors);

        return new AdvanceDirectiveDocument(pid, type, originalFileName, mimeType, content, effectiveDate, ownerId);
    }

    internal void AssignGeneratedIdentity(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException($"Advance directive document {Uuid} already has an assigned identity.");

        Id = id;
    }
}
