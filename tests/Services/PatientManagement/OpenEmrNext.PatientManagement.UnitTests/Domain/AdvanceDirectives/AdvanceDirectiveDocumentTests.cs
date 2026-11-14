using System.Security.Cryptography;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;

namespace OpenEmrNext.PatientManagement.UnitTests.Domain.AdvanceDirectives;

public class AdvanceDirectiveDocumentTests
{
    private static readonly byte[] Content = [1, 2, 3, 4];

    [Fact]
    public void Upload_with_valid_fields_succeeds()
    {
        var document = AdvanceDirectiveDocument.Upload(
            pid: 1, AdvanceDirectiveType.LivingWill, "consent.pdf", "application/pdf", Content, null, ownerId: 7);

        Assert.Equal(1, document.Pid);
        Assert.Equal(AdvanceDirectiveType.LivingWill, document.Type);
        Assert.Equal("Living Will - consent.pdf", document.Name);
        Assert.Equal("application/pdf", document.MimeType);
        Assert.Equal(Content.Length, document.Size);
        Assert.Equal(7, document.OwnerId);
        Assert.Equal(0, document.Id);
        Assert.NotEqual(Guid.Empty, document.Uuid);
        Assert.NotEqual(Guid.Empty, document.DriveUuid);
        Assert.NotEqual(document.Uuid, document.DriveUuid);
        Assert.Equal(Convert.ToHexStringLower(SHA3_512.HashData(Content)), document.Sha3Hash);
    }

    [Fact]
    public void Upload_rejects_a_pid_that_cannot_identify_a_patient() =>
        Assert.Throws<AdvanceDirectiveDocumentValidationException>(
            () => AdvanceDirectiveDocument.Upload(0, AdvanceDirectiveType.LivingWill, "consent.pdf", "application/pdf", Content, null, 7));

    [Fact]
    public void Upload_rejects_an_owner_id_that_cannot_identify_a_user() =>
        Assert.Throws<AdvanceDirectiveDocumentValidationException>(
            () => AdvanceDirectiveDocument.Upload(1, AdvanceDirectiveType.LivingWill, "consent.pdf", "application/pdf", Content, null, 0));

    [Fact]
    public void Upload_rejects_an_empty_file_name() =>
        Assert.Throws<AdvanceDirectiveDocumentValidationException>(
            () => AdvanceDirectiveDocument.Upload(1, AdvanceDirectiveType.LivingWill, "", "application/pdf", Content, null, 7));

    [Fact]
    public void Upload_rejects_a_blank_mime_type() =>
        Assert.Throws<AdvanceDirectiveDocumentValidationException>(
            () => AdvanceDirectiveDocument.Upload(1, AdvanceDirectiveType.LivingWill, "consent.pdf", " ", Content, null, 7));

    [Fact]
    public void Upload_rejects_empty_content() =>
        Assert.Throws<AdvanceDirectiveDocumentValidationException>(
            () => AdvanceDirectiveDocument.Upload(1, AdvanceDirectiveType.LivingWill, "consent.pdf", "application/pdf", [], null, 7));

    [Fact]
    public void AssignGeneratedIdentity_cannot_run_twice()
    {
        var document = AdvanceDirectiveDocument.Upload(1, AdvanceDirectiveType.LivingWill, "consent.pdf", "application/pdf", Content, null, 7);
        document.AssignGeneratedIdentity(10);

        Assert.Throws<InvalidOperationException>(() => document.AssignGeneratedIdentity(11));
    }
}
