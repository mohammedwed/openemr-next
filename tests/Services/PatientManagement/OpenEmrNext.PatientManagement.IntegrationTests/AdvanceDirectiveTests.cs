using System.Text;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ReadModels;
using OpenEmrNext.PatientManagement.Infrastructure;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Proves advance-directive persistence against a real MariaDB and a real local disk: uploading a
/// document, reading it back (including legacy's own name-based type classification), downloading its
/// content, the shared <c>sequences</c> counter actually handing out distinct ids, and the status-flag
/// round trip.
/// </summary>
[Collection("PatientDataContainer")]
public class AdvanceDirectiveTests(PatientDataContainerFixture fixture) : IDisposable
{
    private static long NextPid() => Random.Shared.NextInt64(1, long.MaxValue);
    private readonly DirectoryInfo _repositoryRoot = Directory.CreateTempSubdirectory("advance-directive-tests-");

    public void Dispose() => _repositoryRoot.Delete(recursive: true);

    private EfAdvanceDirectiveRepository CreateRepository() =>
        new(fixture.CreateDbContext(), new LocalDiskAdvanceDirectiveFileStore(_repositoryRoot.FullName));

    /// <summary>The status flags live on <c>patient_data</c> -- unlike documents, which have no foreign
    /// key, setting them for a pid with no row there is a silent no-op, so these tests need a real
    /// registered patient, the same way <c>DuplicateScoringTests</c> does.</summary>
    private static async Task<long> RegisterPatient(PatientManagementDbContext context)
    {
        var registration = PatientRegistration.Create(
            "Test", $"Patient{Guid.NewGuid():N}", $"TestSex{Guid.NewGuid():N}"[..30], new DateOnly(2000, 1, 1), null, null);
        var patient = await new RegisterPatientUseCase(new EfPatientRegistry(context))
            .ExecuteAsync(registration, registeredBy: 1, DateTime.UtcNow, CancellationToken.None);
        return patient.Pid;
    }

    [Fact]
    public async Task Uploading_a_document_makes_it_appear_in_the_patients_listing()
    {
        var pid = NextPid();
        var repository = CreateRepository();
        var content = Encoding.UTF8.GetBytes("a living will, in plain text for this test");

        var uploaded = await new UploadAdvanceDirectiveDocumentUseCase(repository).ExecuteAsync(
            pid, AdvanceDirectiveType.LivingWill, "consent.pdf", "application/pdf", content, new DateOnly(2026, 1, 1), ownerId: 7, CancellationToken.None);

        var documents = await new GetAdvanceDirectiveDocumentsUseCase(repository).ExecuteAsync(pid, CancellationToken.None);

        var document = Assert.Single(documents);
        Assert.Equal(uploaded.Uuid, document.Uuid);
        Assert.Equal(AdvanceDirectiveType.LivingWill, document.Type);
        Assert.Equal("Living Will - consent.pdf", document.Name);
        Assert.Equal("application/pdf", document.MimeType);
        Assert.Equal(content.Length, document.Size);
        Assert.Equal(new DateOnly(2026, 1, 1), document.EffectiveDate);
        Assert.Equal(7, document.OwnerId);
    }

    [Fact]
    public async Task Downloading_a_document_returns_the_exact_bytes_uploaded()
    {
        var pid = NextPid();
        var repository = CreateRepository();
        var content = Encoding.UTF8.GetBytes("durable power of attorney scan bytes");

        var uploaded = await new UploadAdvanceDirectiveDocumentUseCase(repository).ExecuteAsync(
            pid, AdvanceDirectiveType.DurablePowerOfAttorney, "poa.pdf", "application/pdf", content, null, 7, CancellationToken.None);

        var downloaded = await new DownloadAdvanceDirectiveDocumentUseCase(repository).ExecuteAsync(pid, uploaded.Uuid, CancellationToken.None);

        Assert.NotNull(downloaded);
        Assert.Equal(content, downloaded.Value.Content);
        Assert.Equal("application/pdf", downloaded.Value.MimeType);
        Assert.Equal("Durable Power of Attorney - poa.pdf", downloaded.Value.FileName);
    }

    [Fact]
    public async Task Downloading_with_the_wrong_pid_returns_nothing()
    {
        var pid = NextPid();
        var repository = CreateRepository();

        var uploaded = await new UploadAdvanceDirectiveDocumentUseCase(repository).ExecuteAsync(
            pid, AdvanceDirectiveType.DnrOrder, "dnr.pdf", "application/pdf", [1, 2, 3], null, 7, CancellationToken.None);

        var downloaded = await new DownloadAdvanceDirectiveDocumentUseCase(repository)
            .ExecuteAsync(pid: NextPid(), uploaded.Uuid, CancellationToken.None);

        Assert.Null(downloaded);
    }

    [Fact]
    public async Task Two_uploads_for_the_same_patient_get_distinct_ids_from_the_shared_sequence()
    {
        var pid = NextPid();
        var repository = CreateRepository();

        var first = await new UploadAdvanceDirectiveDocumentUseCase(repository).ExecuteAsync(
            pid, AdvanceDirectiveType.LivingWill, "a.pdf", "application/pdf", [1], null, 7, CancellationToken.None);
        var second = await new UploadAdvanceDirectiveDocumentUseCase(repository).ExecuteAsync(
            pid, AdvanceDirectiveType.DnrOrder, "b.pdf", "application/pdf", [2], null, 7, CancellationToken.None);

        Assert.NotEqual(0, first.Id);
        Assert.NotEqual(0, second.Id);
        Assert.NotEqual(first.Id, second.Id);

        var documents = await new GetAdvanceDirectiveDocumentsUseCase(repository).ExecuteAsync(pid, CancellationToken.None);
        Assert.Equal(2, documents.Count);
    }

    [Fact]
    public async Task Setting_and_reading_back_the_status_flags_round_trips()
    {
        var repository = CreateRepository();
        var pid = await RegisterPatient(fixture.CreateDbContext());
        var reviewedAt = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc);

        await new SetAdvanceDirectiveStatusUseCase(repository)
            .ExecuteAsync(pid, AdvanceDirectiveCompletionStatus.Yes, reviewedAt, authenticatorUserId: 3, CancellationToken.None);

        var (status, reviewedAtUtc, authenticatorUserId) = await new GetAdvanceDirectiveStatusUseCase(repository)
            .ExecuteAsync(pid, CancellationToken.None);

        Assert.Equal(AdvanceDirectiveCompletionStatus.Yes, status);
        Assert.Equal(reviewedAt, reviewedAtUtc);
        Assert.Equal(3, authenticatorUserId);
    }

    [Fact]
    public async Task A_patient_with_no_status_set_yet_reads_back_as_the_legacy_default()
    {
        var pid = NextPid();
        var repository = CreateRepository();

        var (status, reviewedAtUtc, authenticatorUserId) = await new GetAdvanceDirectiveStatusUseCase(repository)
            .ExecuteAsync(pid, CancellationToken.None);

        // No patient_data row exists for a pid nothing has registered -- same "nothing to report" shape
        // as an existing row whose completed_ad is still the legacy default ('NO' maps elsewhere; an
        // absent row is simply Unknown here, not a thrown exception).
        Assert.Equal(AdvanceDirectiveCompletionStatus.Unknown, status);
        Assert.Null(reviewedAtUtc);
        Assert.Null(authenticatorUserId);
    }
}
