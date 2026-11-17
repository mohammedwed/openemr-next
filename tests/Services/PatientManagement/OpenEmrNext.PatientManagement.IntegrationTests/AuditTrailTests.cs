using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;
using OpenEmrNext.PatientManagement.Infrastructure;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Patient Management's audit trail (SEC-9, ADR 0078) against a real MariaDB: every write leaves one row naming who did what to which record, committed in the write's own transaction, with field names and identifiers
/// and nothing about the patient; a refusal leaves a <c>failed</c> row; and a write whose audit row cannot be written does not happen.
/// </summary>
[Collection("PatientDataContainer")]
public class AuditTrailTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private DirectoryInfo _root = null!;

    public async Task InitializeAsync()
    {
        _root = Directory.CreateTempSubdirectory("pm-audit");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            [
                new("PatientManagementDb:Host", fixture.Hostname),
                new("PatientManagementDb:Port", fixture.Port.ToString()),
                new("PatientManagementDb:Database", fixture.Database),
                new("PatientManagementDb:User", fixture.User),
                new("PatientManagementDb:Password", fixture.DbPassword),
                new("AdvanceDirectiveStorage:RepositoryRoot", _root.FullName),
                new("InternalGateway:Secret", "test-secret")
            ])));
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Internal-Gateway-Secret", "test-secret");
        _client.DefaultRequestHeaders.Add("X-Authenticated-User", PatientDataContainerFixture.TestUserUuid.ToString());
        _client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.9, 10.0.0.1");
        await Exec("DELETE FROM patient_management_audit");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory?.Dispose();
        _root.Delete(recursive: true);
        return Task.CompletedTask;
    }

    private async Task Exec(string sql)
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T?> Scalar<T>(string sql)
    {
        await using var connection = new MySqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    private Task<long> Audits(string where = "1=1") => Scalar<long>($"SELECT COUNT(*) FROM patient_management_audit WHERE {where}")!;

    private static long NextPid() => Random.Shared.NextInt64(1, long.MaxValue);

    private static readonly AuditOrigin Nurse = new(new AuditActor("7e57c0de-0000-4000-8000-000000000007", 7, true), "trace-1", "192.0.2.1");

    // Through the HTTP pipeline: the verified user, the forwarded address and the trace id reach the row

    [Fact]
    public async Task Registering_a_patient_writes_one_success_row_naming_the_verified_user_the_patient_and_where_it_came_from()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest("Audit", $"Trail{Guid.NewGuid():N}", "Female", new DateOnly(1980, 1, 1), null, null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var registered = (await response.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!;
        Assert.Equal(1L, await Audits());
        Assert.Equal(1L, await Audits($"action = 'patient.register' AND outcome = 'success' AND subject = '{registered.Uuid}'"));
        Assert.Equal(PatientDataContainerFixture.TestUserUuid.ToString(), await Scalar<string>("SELECT actor FROM patient_management_audit"));
        Assert.Equal(PatientDataContainerFixture.TestUserId, await Scalar<long>("SELECT actor_id FROM patient_management_audit"));
        Assert.Equal(1L, await Scalar<long>("SELECT actor_verified FROM patient_management_audit"));
        Assert.Equal("203.0.113.9", await Scalar<string>("SELECT source_address FROM patient_management_audit"));
        Assert.False(string.IsNullOrWhiteSpace(await Scalar<string>("SELECT correlation_id FROM patient_management_audit")));
    }

    [Fact]
    public async Task A_patient_registered_through_fhir_is_audited_the_same_way()
    {
        var body = new StringContent(
            $$"""{"resourceType":"Patient","name":[{"use":"official","family":"Fhir{{Guid.NewGuid():N}}","given":["Audit"]}],"gender":"female","birthDate":"1988-08-08","telecom":[{"system":"email","use":"home","value":"audit-fhir@example.com"}]}""",
            Encoding.UTF8, "application/fhir+json");

        var response = await _client.PostAsync("/fhir/Patient", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1L, await Audits("action = 'patient.register' AND outcome = 'success'"));
        Assert.Equal(PatientDataContainerFixture.TestUserUuid.ToString(), await Scalar<string>("SELECT actor FROM patient_management_audit"));
    }

    [Fact]
    public async Task Adding_and_swapping_insurance_are_each_audited_under_the_policy_they_touched()
    {
        var pid = NextPid();
        var primary = (await (await _client.PostAsJsonAsync($"/api/v1/patients/{pid}/insurance", new AddInsurancePolicyRequest("primary", 7, "POL100", null, null, new DateOnly(2026, 1, 1), null)))
            .Content.ReadFromJsonAsync<InsurancePolicyAddedResponse>())!;

        var swap = await _client.PostAsJsonAsync($"/api/v1/patients/{pid}/insurance/swap", new SwapInsuranceRequest("secondary", primary.Uuid));

        Assert.Equal(HttpStatusCode.NoContent, swap.StatusCode);
        Assert.Equal(1L, await Audits($"action = 'insurance.add' AND outcome = 'success' AND subject = '{primary.Uuid}'"));
        Assert.Equal(1L, await Audits($"action = 'insurance.swap' AND outcome = 'success' AND subject = '{primary.Uuid}'"));
    }

    [Fact]
    public async Task Uploading_a_directive_document_and_setting_its_status_are_audited()
    {
        var registered = (await (await _client.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest("Dir", $"Ective{Guid.NewGuid():N}", "Male", new DateOnly(1970, 1, 1), null, null, null)))
            .Content.ReadFromJsonAsync<PatientRegisteredResponse>())!;
        await Exec("DELETE FROM patient_management_audit");

        var upload = await _client.PostAsJsonAsync($"/api/v1/patients/{registered.Pid}/advance-directives/documents",
            new UploadAdvanceDirectiveDocumentRequest("living_will", "will.pdf", "application/pdf", Convert.ToBase64String([1, 2, 3]), new DateOnly(2026, 1, 1), null));
        var document = (await upload.Content.ReadFromJsonAsync<AdvanceDirectiveDocumentResponse>())!;
        var status = await _client.PutAsJsonAsync($"/api/v1/patients/{registered.Pid}/advance-directives/status", new SetAdvanceDirectiveStatusRequest("yes", DateTime.UtcNow, null));

        Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);
        Assert.Equal(1L, await Audits($"action = 'directive.document.upload' AND outcome = 'success' AND subject = '{document.Uuid}'"));
        Assert.Equal(1L, await Audits($"action = 'directive.status.set' AND outcome = 'success' AND subject = 'patient:{registered.Pid}' AND details = '[\"status\"]'"));
    }

    [Fact]
    public async Task Setting_the_status_of_a_patient_that_does_not_exist_is_404_and_audited_as_failed()
    {
        var pid = NextPid();

        var response = await _client.PutAsJsonAsync($"/api/v1/patients/{pid}/advance-directives/status", new SetAdvanceDirectiveStatusRequest("yes", null, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0L, await Audits("outcome = 'success'"));
        Assert.Equal(1L, await Audits($"action = 'directive.status.set' AND outcome = 'failed' AND subject = 'patient:{pid}'"));
    }

    [Fact]
    public async Task A_request_with_no_verified_user_writes_nothing_and_leaves_no_audit_row()
    {
        using var anonymous = _factory!.CreateClient();
        anonymous.DefaultRequestHeaders.Add("X-Internal-Gateway-Secret", "test-secret");

        var response = await anonymous.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest("No", "User", "Female", new DateOnly(1980, 1, 1), null, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0L, await Audits());
    }

    [Fact]
    public async Task Reading_is_not_audited()
    {
        var registered = (await (await _client.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest("Read", $"Only{Guid.NewGuid():N}", "Female", new DateOnly(1980, 1, 1), null, null, null)))
            .Content.ReadFromJsonAsync<PatientRegisteredResponse>())!;
        await Exec("DELETE FROM patient_management_audit");

        await _client.GetAsync($"/api/v1/patients/{registered.Uuid}");
        await _client.GetAsync($"/api/v1/patients/{registered.Pid}/insurance");
        await _client.GetAsync($"/api/v1/patients/{registered.Pid}/advance-directives/status");
        await _client.GetAsync("/fhir/Patient");

        Assert.Equal(0L, await Audits());
    }

    // Through the use cases: refusals and atomicity

    private (RegisterPatientUseCase Register, AddInsurancePolicyUseCase Add, SwapInsuranceUseCase Swap, UploadAdvanceDirectiveDocumentUseCase Upload) UseCases()
    {
        var db = fixture.CreateDbContext();
        var log = new EfAuditLog(db);
        var insurance = new EfInsurancePolicyRepository(db, log);
        var directives = new EfAdvanceDirectiveRepository(db, new LocalDiskAdvanceDirectiveFileStore(_root.FullName), log);
        return (new RegisterPatientUseCase(new EfPatientRegistry(db, log), log), new AddInsurancePolicyUseCase(insurance, log), new SwapInsuranceUseCase(insurance, log), new UploadAdvanceDirectiveDocumentUseCase(directives, log));
    }

    [Fact]
    public async Task A_registration_the_domain_refuses_writes_a_failed_row_and_no_patient()
    {
        var before = await Scalar<long>("SELECT COUNT(*) FROM patient_data");

        await Assert.ThrowsAsync<PatientValidationException>(() => UseCases().Register.ExecuteAsync("", "X", "no", new DateOnly(2020, 1, 1), null, null, 7, DateTime.UtcNow, CancellationToken.None, Nurse));

        Assert.Equal(before, await Scalar<long>("SELECT COUNT(*) FROM patient_data"));
        Assert.Equal(1L, await Audits("action = 'patient.register' AND outcome = 'failed' AND subject = 'unregistered' AND actor_verified = 1"));
        Assert.Equal(0L, await Audits("outcome = 'success'"));
    }

    [Fact]
    public async Task An_insurance_policy_the_domain_refuses_writes_a_failed_row_under_the_patient_and_no_policy()
    {
        var pid = NextPid();

        await Assert.ThrowsAsync<OpenEmrNext.PatientManagement.Domain.Insurance.InsurancePolicyValidationException>(() =>
            UseCases().Add.ExecuteAsync(pid, InsuranceLevel.Primary, 7, "POL200", null, null, new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1), CancellationToken.None, Nurse));

        Assert.Equal(0L, await Scalar<long>($"SELECT COUNT(*) FROM insurance_data WHERE pid = {pid}"));
        Assert.Equal(1L, await Audits($"action = 'insurance.add' AND outcome = 'failed' AND subject = 'patient:{pid}'"));
    }

    [Fact]
    public async Task Swapping_a_policy_that_does_not_exist_writes_a_failed_row_under_the_uuid_asked_for()
    {
        var pid = NextPid();
        var missing = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => UseCases().Swap.ExecuteAsync(pid, InsuranceLevel.Secondary, missing, CancellationToken.None, Nurse));

        Assert.Equal(1L, await Audits($"action = 'insurance.swap' AND outcome = 'failed' AND subject = '{missing}'"));
    }

    [Fact]
    public async Task A_document_the_domain_refuses_writes_a_failed_row_and_stores_nothing()
    {
        var pid = NextPid();

        await Assert.ThrowsAsync<OpenEmrNext.PatientManagement.Domain.AdvanceDirectives.AdvanceDirectiveDocumentValidationException>(() =>
            UseCases().Upload.ExecuteAsync(pid, AdvanceDirectiveType.LivingWill, "a.pdf", "", [1], null, 7, CancellationToken.None, Nurse));

        Assert.Equal(0L, await Scalar<long>($"SELECT COUNT(*) FROM documents WHERE foreign_id = {pid}"));
        Assert.Equal(1L, await Audits($"action = 'directive.document.upload' AND outcome = 'failed' AND subject = 'patient:{pid}'"));
    }

    /// <summary>An audit log that cannot be written: stands for a full disk, a revoked grant, a dropped connection.</summary>
    private sealed class BrokenAuditLog : IAuditLog
    {
        public Task AppendAsync(AuditEntry entry, CancellationToken ct) => throw new InvalidOperationException("the audit table is not writable");
    }

    [Fact]
    public async Task A_registration_whose_audit_row_cannot_be_written_does_not_happen()
    {
        var patients = await Scalar<long>("SELECT COUNT(*) FROM patient_data");
        var events = await Scalar<long>("SELECT COUNT(*) FROM patient_management_outbox");
        var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RegisterPatientUseCase(new EfPatientRegistry(db, new BrokenAuditLog()), new BrokenAuditLog())
                .ExecuteAsync("Ghost", "Patient", "female", new DateOnly(1980, 1, 1), null, null, 7, DateTime.UtcNow, CancellationToken.None, Nurse));

        Assert.Equal(patients, await Scalar<long>("SELECT COUNT(*) FROM patient_data"));
        Assert.Equal(events, await Scalar<long>("SELECT COUNT(*) FROM patient_management_outbox"));
    }

    [Fact]
    public async Task An_insurance_policy_whose_audit_row_cannot_be_written_does_not_happen()
    {
        var pid = NextPid();
        var db = fixture.CreateDbContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AddInsurancePolicyUseCase(new EfInsurancePolicyRepository(db, new BrokenAuditLog()), new BrokenAuditLog())
                .ExecuteAsync(pid, InsuranceLevel.Primary, 7, "POL300", null, null, new DateOnly(2026, 1, 1), null, CancellationToken.None, Nurse));

        Assert.Equal(0L, await Scalar<long>($"SELECT COUNT(*) FROM insurance_data WHERE pid = {pid}"));
    }

    [Fact]
    public async Task A_document_whose_audit_row_cannot_be_written_does_not_happen()
    {
        var pid = NextPid();
        var db = fixture.CreateDbContext();
        var repository = new EfAdvanceDirectiveRepository(db, new LocalDiskAdvanceDirectiveFileStore(_root.FullName), new BrokenAuditLog());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UploadAdvanceDirectiveDocumentUseCase(repository, new BrokenAuditLog())
                .ExecuteAsync(pid, AdvanceDirectiveType.LivingWill, "a.pdf", "application/pdf", [1, 2, 3], null, 7, CancellationToken.None, Nurse));

        Assert.Equal(0L, await Scalar<long>($"SELECT COUNT(*) FROM documents WHERE foreign_id = {pid}"));
    }

    [Fact]
    public async Task A_status_whose_audit_row_cannot_be_written_is_not_set()
    {
        var registered = (await (await _client.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest("Status", $"Atomic{Guid.NewGuid():N}", "Female", new DateOnly(1980, 1, 1), null, null, null)))
            .Content.ReadFromJsonAsync<PatientRegisteredResponse>())!;
        var db = fixture.CreateDbContext();
        var repository = new EfAdvanceDirectiveRepository(db, new LocalDiskAdvanceDirectiveFileStore(_root.FullName), new BrokenAuditLog());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SetAdvanceDirectiveStatusUseCase(repository, new BrokenAuditLog()).ExecuteAsync(registered.Pid, AdvanceDirectiveCompletionStatus.Yes, null, null, CancellationToken.None, Nurse));

        var stored = await Scalar<string>($"SELECT completed_ad FROM patient_data WHERE pid = {registered.Pid}");
        Assert.NotEqual("YES", stored?.ToUpperInvariant());
    }

    [Fact]
    public async Task The_trail_holds_identifiers_and_field_names_and_nothing_about_the_patient()
    {
        var registered = (await (await _client.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest("SECRETFIRST", "SECRETLAST", "Female", new DateOnly(1969, 7, 20), "secret@example.com", null, null)))
            .Content.ReadFromJsonAsync<PatientRegisteredResponse>())!;
        await _client.PostAsJsonAsync($"/api/v1/patients/{registered.Pid}/insurance", new AddInsurancePolicyRequest("primary", 7, "SECRETPOLICY", "SECRETGROUP", null, new DateOnly(2026, 1, 1), null));

        var rows = await Scalar<long>(
            "SELECT COUNT(*) FROM patient_management_audit WHERE CONCAT_WS('|', actor, action, subject, IFNULL(details,''), IFNULL(correlation_id,''), IFNULL(source_address,'')) REGEXP 'SECRET|1969|example.com'");
        Assert.Equal(0L, rows);
        Assert.Equal(2L, await Audits());
    }

    [Fact]
    public void A_long_header_is_cut_to_the_column_not_refused()
    {
        var entry = new AuditEntry(DateTime.UtcNow, new AuditActor(new string('a', 500), 7, true), "patient.register", new string('s', 100), AuditOutcome.Success, new string('c', 500), new string('1', 500));

        Assert.Equal(AuditEntry.MaxActorLength, entry.Actor.Subject.Length);
        Assert.Equal(AuditEntry.MaxSubjectLength, entry.Subject.Length);
        Assert.Equal(AuditEntry.MaxCorrelationLength, entry.CorrelationId!.Length);
        Assert.Equal(AuditEntry.MaxAddressLength, entry.SourceAddress!.Length);
    }
}
