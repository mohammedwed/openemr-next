using System.Text;
using MySqlConnector;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;
using OpenEmrNext.PatientManagement.Infrastructure;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Finding F-CC-3: legacy's <c>UuidRegistry::createUuid</c> records every uuid it hands out in <c>uuid_registry</c>;
/// PatientManagement did not. Each uuid it creates is now recorded in the same transaction as the row, and finding F-CC-9
/// (the shared counter's lock) must not have broken document uploads.
/// </summary>
[Collection("PatientDataContainer")]
public class UuidRegistryTests(PatientDataContainerFixture fixture) : IDisposable
{
    private readonly DirectoryInfo _repositoryRoot = Directory.CreateTempSubdirectory("uuid-registry-tests-");
    public void Dispose() => _repositoryRoot.Delete(recursive: true);

    private async Task<string?> RegistryTableForAsync(Guid uuid)
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT table_name FROM uuid_registry WHERE HEX(uuid) = '{uuid.ToString("N").ToUpperInvariant()}'";
        return await command.ExecuteScalarAsync() as string;
    }

    [Fact]
    public async Task Registering_a_patient_records_its_uuid_as_patient_data()
    {
        await using var context = fixture.CreateDbContext();

        var patient = await new RegisterPatientUseCase(new EfPatientRegistry(context, new EfAuditLog(context)), new EfAuditLog(context)).ExecuteAsync(
            PatientRegistration.Create("Ada", "Lovelace", "Female", new DateOnly(1990, 1, 1)), 1, DateTime.UtcNow, CancellationToken.None);

        Assert.Equal("patient_data", await RegistryTableForAsync(patient.Uuid));
    }

    [Fact]
    public async Task Adding_an_insurance_policy_records_its_uuid_as_insurance_data()
    {
        await using var context = fixture.CreateDbContext();
        var repository = new EfInsurancePolicyRepository(context, new EfAuditLog(context));

        var policy = await new AddInsurancePolicyUseCase(repository, new EfAuditLog(context)).ExecuteAsync(
            Random.Shared.NextInt64(1, long.MaxValue), InsuranceLevel.Primary, 7, "POL1", null, null, new DateOnly(2020, 1, 1), null, CancellationToken.None);

        Assert.Equal("insurance_data", await RegistryTableForAsync(policy.Uuid));
    }

    [Fact]
    public async Task Uploading_a_document_records_its_uuid_as_documents()
    {
        var db = fixture.CreateDbContext();
        var repository = new EfAdvanceDirectiveRepository(db, new LocalDiskAdvanceDirectiveFileStore(_repositoryRoot.FullName), new EfAuditLog(db));

        var document = await new UploadAdvanceDirectiveDocumentUseCase(repository, new EfAuditLog(fixture.CreateDbContext())).ExecuteAsync(
            Random.Shared.NextInt64(1, long.MaxValue), AdvanceDirectiveType.LivingWill, "a.pdf", "application/pdf",
            Encoding.UTF8.GetBytes("text"), new DateOnly(2026, 1, 1), 7, CancellationToken.None);

        Assert.Equal("documents", await RegistryTableForAsync(document.Uuid));
    }

    [Fact]
    public async Task F_CC_9_concurrent_uploads_all_succeed_with_distinct_ids_now_that_the_counter_is_drawn_in_its_own_transaction()
    {
        var uploads = Enumerable.Range(0, 12).Select(async i =>
        {
            var db = fixture.CreateDbContext();
        var repository = new EfAdvanceDirectiveRepository(db, new LocalDiskAdvanceDirectiveFileStore(_repositoryRoot.FullName), new EfAuditLog(db));
            return await new UploadAdvanceDirectiveDocumentUseCase(repository, new EfAuditLog(fixture.CreateDbContext())).ExecuteAsync(
                Random.Shared.NextInt64(1, long.MaxValue), AdvanceDirectiveType.LivingWill, $"d{i}.pdf", "application/pdf",
                Encoding.UTF8.GetBytes($"document {i}"), new DateOnly(2026, 1, 1), 7, CancellationToken.None);
        });

        var documents = await Task.WhenAll(uploads);

        Assert.Equal(12, documents.Select(d => d.Id).Distinct().Count());
    }
}
