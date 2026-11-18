using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;
using OpenEmrNext.PatientManagement.Infrastructure;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Correcting a patient's name, date of birth, sex and emails (M5r, ADR 0081) through the real HTTP pipeline against a real MariaDB: the conditional-write contract, that only the six owned columns are ever written,
/// that a legacy edit of an owned column is a conflict and an edit of anything else is not, the audit row, and the duplicate score.
/// </summary>
[Collection("PatientDataContainer")]
public class PatientDemographicsTests(PatientDataContainerFixture fixture) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private DirectoryInfo _root = null!;

    public async Task InitializeAsync()
    {
        _root = Directory.CreateTempSubdirectory("pm-demographics");
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

    private static string Unique() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// A date of birth no other test uses (the 1700s). The database is shared by every test class and the duplicate score compares dates of birth, so a constant date shared with another class's patients would make
    /// them each other's duplicates.
    /// </summary>
    private static DateOnly UniqueDob() => new(1700 + Random.Shared.Next(0, 100), Random.Shared.Next(1, 13), Random.Shared.Next(1, 28));

    private readonly DateOnly _newDob = UniqueDob();

    private async Task<PatientRegisteredResponse> RegisterAsync(string? last = null, string sex = "Female", DateOnly? dob = null, string? email = "unset", string? trusted = "unset")
    {
        // "unset" means a unique address; null means none. Shared addresses would also make patients each other's duplicates.
        email = email == "unset" ? $"first{Unique()}@example.com" : email;
        trusted = trusted == "unset" ? $"direct{Unique()}@example.com" : trusted;
        var response = await _client.PostAsJsonAsync("/api/v1/patients", new RegisterPatientRequest("Ada", last ?? $"Lovelace{Unique()}", sex, dob ?? UniqueDob(), email, trusted, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PatientRegisteredResponse>())!;
    }

    private async Task<(PatientSummaryResponse Patient, string ETag)> ReadAsync(Guid uuid)
    {
        var response = await _client.GetAsync($"/api/v1/patients/{uuid}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ((await response.Content.ReadFromJsonAsync<PatientSummaryResponse>())!, response.Headers.ETag!.ToString());
    }

    private UpdatePatientRequest Change(string first = "Augusta", string? last = null, string sex = "Female", DateOnly? dob = null, string? email = "unset", string? trusted = "unset") =>
        new(first, last ?? $"King{_newDob:yyyyMMdd}", sex, dob ?? _newDob, email == "unset" ? $"ada{Unique()}@example.com" : email, trusted == "unset" ? $"direct{Unique()}@example.com" : trusted);

    private Task<HttpResponseMessage> PutAsync(Guid uuid, UpdatePatientRequest body, string? ifMatch, HttpClient? client = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/patients/{uuid}") { Content = JsonContent.Create(body) };
        if (ifMatch is not null)
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return (client ?? _client).SendAsync(request);
    }

    private Task<long> Audits(string where = "1=1") => Scalar<long>($"SELECT COUNT(*) FROM patient_management_audit WHERE {where}")!;

    // The read side

    [Fact]
    public async Task A_patient_read_carries_a_strong_etag_and_every_editable_field_including_sex_and_the_trusted_email()
    {
        var registered = await RegisterAsync(sex: "Male", trusted: "direct-exact@example.com");

        var (patient, etag) = await ReadAsync(registered.Uuid);

        Assert.Matches("^\"p-[0-9a-f]{32}\"$", etag);
        Assert.Equal(etag, patient.ETag);
        Assert.Equal("Male", patient.Sex);
        Assert.Equal("direct-exact@example.com", patient.TrustedEmail);   // a form built on this read must be able to send all six fields back
        Assert.StartsWith("first", patient.Email);
    }

    [Fact]
    public async Task The_version_does_not_move_when_nothing_this_service_owns_changes()
    {
        var registered = await RegisterAsync();
        var (_, before) = await ReadAsync(registered.Uuid);

        await Exec($"UPDATE patient_data SET phone_home = '555-0100', street = '1 Main St', ss = '123-45-6789' WHERE uuid = UNHEX('{registered.Uuid:N}')");

        Assert.Equal(before, (await ReadAsync(registered.Uuid)).ETag);
    }

    // The correction

    [Fact]
    public async Task A_correction_based_on_the_current_version_changes_the_six_fields_and_answers_with_the_new_patient_and_version()
    {
        var registered = await RegisterAsync();
        var (_, etag) = await ReadAsync(registered.Uuid);

        var response = await PutAsync(registered.Uuid, Change(), etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<PatientSummaryResponse>())!;
        Assert.Equal(("Augusta", $"King{_newDob:yyyyMMdd}", "Female"), (body.FirstName, body.LastName, body.Sex));
        Assert.Equal(_newDob, body.DateOfBirth);
        Assert.StartsWith("ada", body.Email);
        Assert.NotEqual(etag, response.Headers.ETag!.ToString());
        Assert.Equal(response.Headers.ETag.ToString(), (await ReadAsync(registered.Uuid)).ETag);
        Assert.Equal($"King{_newDob:yyyyMMdd}", await Scalar<string>($"SELECT lname FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')"));
        Assert.Equal(_newDob.ToString("yyyy-MM-dd"), await Scalar<string>($"SELECT CAST(DOB AS CHAR) FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')"));
    }

    [Fact]
    public async Task A_correction_replaces_all_six_so_a_field_sent_empty_is_cleared()
    {
        var registered = await RegisterAsync();
        var (_, etag) = await ReadAsync(registered.Uuid);

        var response = await PutAsync(registered.Uuid, Change(email: null, trusted: null), etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (patient, _) = await ReadAsync(registered.Uuid);
        Assert.Null(patient.Email);
        Assert.Null(patient.TrustedEmail);
        Assert.Equal("", await Scalar<string>($"SELECT email FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')"));
    }

    [Fact]
    public async Task A_correction_writes_only_the_columns_this_service_owns_and_leaves_everything_else_as_other_writers_have_it()
    {
        var registered = await RegisterAsync();
        var where = $"uuid = UNHEX('{registered.Uuid:N}')";
        await Exec($"UPDATE patient_data SET phone_home = '555-0100', phone_cell = '555-0199', street = '1 Main St', city = 'Springfield', ss = '123-45-6789', mname = 'Byron', race = 'white', language = 'eng' WHERE {where}");
        var (_, etag) = await ReadAsync(registered.Uuid);
        var othersBefore = await Scalar<string>(
            $"SELECT CONCAT_WS('|', pubpid, phone_home, phone_cell, street, city, ss, mname, race, language, regdate, pid, HEX(uuid)) FROM patient_data WHERE {where}");

        Assert.Equal(HttpStatusCode.OK, (await PutAsync(registered.Uuid, Change(), etag)).StatusCode);

        Assert.Equal(othersBefore, await Scalar<string>(
            $"SELECT CONCAT_WS('|', pubpid, phone_home, phone_cell, street, city, ss, mname, race, language, regdate, pid, HEX(uuid)) FROM patient_data WHERE {where}"));
    }

    [Fact]
    public async Task A_correction_moves_the_tables_own_last_updated_date_to_now()
    {
        var registered = await RegisterAsync();
        await Exec($"UPDATE patient_data SET `date` = '2001-01-01 00:00:00' WHERE uuid = UNHEX('{registered.Uuid:N}')");
        var (_, etag) = await ReadAsync(registered.Uuid);

        await PutAsync(registered.Uuid, Change(), etag);

        Assert.True(await Scalar<DateTime>($"SELECT `date` FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')") > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task A_correction_that_changes_nothing_is_a_200_and_writes_no_audit_row_and_keeps_the_version()
    {
        var registered = await RegisterAsync();
        var (patient, etag) = await ReadAsync(registered.Uuid);
        await Exec("DELETE FROM patient_management_audit");

        var response = await PutAsync(registered.Uuid, new UpdatePatientRequest(patient.FirstName, patient.LastName, patient.Sex!, patient.DateOfBirth!.Value, patient.Email, patient.TrustedEmail), etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(etag, response.Headers.ETag!.ToString());
        Assert.Equal(0L, await Audits());
    }

    // The conditional-write contract

    [Theory]
    [InlineData(null)]
    [InlineData("*")]
    [InlineData("   ")]
    public async Task A_correction_with_no_usable_if_match_is_428_and_changes_nothing(string? ifMatch)
    {
        var registered = await RegisterAsync();
        var (_, before) = await ReadAsync(registered.Uuid);

        var response = await PutAsync(registered.Uuid, Change(), ifMatch);

        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        Assert.Equal(before, (await ReadAsync(registered.Uuid)).ETag);
        Assert.Equal(0L, await Audits("action = 'patient.update'"));
    }

    [Theory]
    [InlineData("\"not-a-version\"")]
    [InlineData("\"c-00000000000000000000000000000000\"")]    // another kind of record's tag can never match
    [InlineData("\"p-00000000000000000000000000000000\"")]    // the right shape, the wrong patient state
    public async Task A_version_that_is_not_the_current_one_is_412_with_the_current_version_and_changes_nothing(string offered)
    {
        var registered = await RegisterAsync();
        var (_, current) = await ReadAsync(registered.Uuid);

        var response = await PutAsync(registered.Uuid, Change(), offered);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(current, response.Headers.ETag?.ToString());
        Assert.NotEqual($"King{_newDob:yyyyMMdd}", await Scalar<string>($"SELECT lname FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')"));
        Assert.Equal(1L, await Audits("action = 'patient.update' AND outcome = 'failed'"));
    }

    [Fact]
    public async Task A_legacy_edit_of_an_owned_column_since_the_read_is_a_conflict_and_is_never_overwritten()
    {
        var registered = await RegisterAsync();
        var (_, etag) = await ReadAsync(registered.Uuid);
        await Exec($"UPDATE patient_data SET lname = 'Edited In Legacy' WHERE uuid = UNHEX('{registered.Uuid:N}')");

        var response = await PutAsync(registered.Uuid, Change(), etag);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal("Edited In Legacy", await Scalar<string>($"SELECT lname FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')"));
    }

    [Fact]
    public async Task A_legacy_edit_of_a_column_this_service_does_not_own_is_not_a_conflict()
    {
        var registered = await RegisterAsync();
        var (_, etag) = await ReadAsync(registered.Uuid);
        await Exec($"UPDATE patient_data SET phone_home = '555-0100', city = 'Springfield' WHERE uuid = UNHEX('{registered.Uuid:N}')");

        var response = await PutAsync(registered.Uuid, Change(), etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("555-0100", await Scalar<string>($"SELECT phone_home FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')"));   // and what legacy wrote is still there
    }

    [Fact]
    public async Task A_patient_that_does_not_exist_is_404_and_audited_as_failed_and_permission_to_look_comes_after_the_precondition()
    {
        var unknown = Guid.NewGuid();

        var withTag = await PutAsync(unknown, Change(), "\"p-00000000000000000000000000000000\"");
        var withoutTag = await PutAsync(unknown, Change(), null);

        Assert.Equal(HttpStatusCode.NotFound, withTag.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionRequired, withoutTag.StatusCode);   // the same order as every write: 428 before anything is looked up
        Assert.Equal(1L, await Audits($"action = 'patient.update' AND outcome = 'failed' AND subject = '{unknown}'"));
    }

    [Theory]
    [InlineData("", "Fine")]             // a first name needs at least one character
    [InlineData("Ada", "L")]             // a one-letter surname is below registration's own minimum
    [InlineData("Ada", "")]
    public async Task A_field_that_fails_registrations_rules_is_400_and_nothing_changes(string first, string last)
    {
        var registered = await RegisterAsync();
        var (_, etag) = await ReadAsync(registered.Uuid);

        var response = await PutAsync(registered.Uuid, Change(first: first, last: last), etag);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(etag, (await ReadAsync(registered.Uuid)).ETag);
    }

    [Fact]
    public async Task Six_simultaneous_corrections_from_one_version_succeed_once()
    {
        var registered = await RegisterAsync();
        var (_, etag) = await ReadAsync(registered.Uuid);

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => PutAsync(registered.Uuid, Change(first: $"Racer{i}"), etag)));

        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(results.Where(r => r.StatusCode != HttpStatusCode.OK), r => Assert.Equal(HttpStatusCode.PreconditionFailed, r.StatusCode));
        Assert.Matches("^Racer[0-5]$", (await ReadAsync(registered.Uuid)).Patient.FirstName);
        Assert.Equal(1L, await Audits("action = 'patient.update' AND outcome = 'success'"));
    }

    // The check under the lock: the use case checks first, so these call the store itself

    private DemographicsUpdateRequest StoreRequest(Guid uuid, string offered) =>
        new(uuid, PatientDemographics.Create("Augusta", "Raced", "Female", _newDob, null, null), [offered], DateTime.UtcNow,
            new AuditEntry(DateTime.UtcNow, AuditActor.Unknown, AuditActions.PatientUpdate, uuid.ToString(), AuditOutcome.Success));

    [Fact]
    public async Task The_store_itself_refuses_a_stale_version_so_the_row_is_protected_by_the_lock_and_not_only_by_the_use_case()
    {
        var registered = await RegisterAsync();
        var db = fixture.CreateDbContext();

        var outcome = await new EfPatientDemographicsStore(db, new EfAuditLog(db)).UpdateAsync(
            StoreRequest(registered.Uuid, "\"p-00000000000000000000000000000000\""), CancellationToken.None);

        Assert.Equal(DemographicsUpdateStatus.Conflict, outcome.Status);
        Assert.NotEqual("Raced", await Scalar<string>($"SELECT lname FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')"));
        Assert.Equal(0L, await Audits("action = 'patient.update'"));
    }

    [Fact]
    public async Task An_edit_that_lands_between_the_read_and_the_write_is_caught_because_the_version_is_checked_under_the_lock()
    {
        var registered = await RegisterAsync();
        var (_, etag) = await ReadAsync(registered.Uuid);
        var db = fixture.CreateDbContext();

        // Another writer holds the row. The correction has read the version it was offered and now waits for the lock; the other writer changes an owned column and lets go.
        await using var legacy = new MySqlConnection(fixture.ConnectionString);
        await legacy.OpenAsync();
        await using var transaction = await legacy.BeginTransactionAsync();
        await using (var hold = new MySqlCommand($"SELECT id FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}') FOR UPDATE", legacy, transaction))
            await hold.ExecuteScalarAsync();

        var waiting = new EfPatientDemographicsStore(db, new EfAuditLog(db)).UpdateAsync(StoreRequest(registered.Uuid, etag), CancellationToken.None);
        await Task.Delay(500);
        Assert.False(waiting.IsCompleted, "the correction should be waiting for the row lock");

        await using (var edit = new MySqlCommand($"UPDATE patient_data SET lname = 'Edited While Waiting' WHERE uuid = UNHEX('{registered.Uuid:N}')", legacy, transaction))
            await edit.ExecuteNonQueryAsync();
        await transaction.CommitAsync();

        var outcome = await waiting;
        Assert.Equal(DemographicsUpdateStatus.Conflict, outcome.Status);
        Assert.Equal("Edited While Waiting", await Scalar<string>($"SELECT lname FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')"));
        Assert.Equal(0L, await Audits("action = 'patient.update'"));
    }

    // Legacy rows

    [Fact]
    public async Task A_legacy_row_with_no_date_of_birth_reads_cleanly_and_can_be_corrected_into_a_complete_one()
    {
        var registered = await RegisterAsync();
        await Exec($"UPDATE patient_data SET DOB = NULL, lname = 'X' WHERE uuid = UNHEX('{registered.Uuid:N}')");

        var (patient, etag) = await ReadAsync(registered.Uuid);
        var response = await PutAsync(registered.Uuid, Change(), etag);

        Assert.Null(patient.DateOfBirth);   // read as it is, never refused for what it holds
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_newDob, (await ReadAsync(registered.Uuid)).Patient.DateOfBirth);
    }

    // Audit

    [Fact]
    public async Task A_correction_writes_one_success_row_naming_the_user_the_patient_and_the_fields_changed_and_nothing_about_them()
    {
        var registered = await RegisterAsync(last: "SECRETSURNAME");
        var (_, etag) = await ReadAsync(registered.Uuid);
        await Exec("DELETE FROM patient_management_audit");

        await PutAsync(registered.Uuid, Change(first: "SECRETFIRST", last: "SECRETLAST", email: "secret@example.com"), etag);

        Assert.Equal(1L, await Audits());
        Assert.Equal(1L, await Audits($"action = 'patient.update' AND outcome = 'success' AND subject = '{registered.Uuid}' AND actor_verified = 1 AND actor_id = {PatientDataContainerFixture.TestUserId}"));
        var fields = await Scalar<string>("SELECT details FROM patient_management_audit");
        Assert.Equal("[\"firstName\",\"lastName\",\"dateOfBirth\",\"email\",\"trustedEmail\"]", fields);
        Assert.Equal(0L, await Scalar<long>("SELECT COUNT(*) FROM patient_management_audit WHERE CONCAT_WS('|', actor, action, subject, IFNULL(details,'')) REGEXP 'SECRET|example.com'"));
    }

    /// <summary>An audit log that cannot be written: a full disk, a revoked grant, a dropped connection.</summary>
    private sealed class BrokenAuditLog : IAuditLog
    {
        public Task AppendAsync(AuditEntry entry, CancellationToken ct) => throw new InvalidOperationException("the audit table is not writable");
    }

    [Fact]
    public async Task A_correction_whose_audit_row_cannot_be_written_does_not_happen()
    {
        var registered = await RegisterAsync();
        var (_, etag) = await ReadAsync(registered.Uuid);
        var db = fixture.CreateDbContext();
        var store = new EfPatientDemographicsStore(db, new BrokenAuditLog());
        var audit = new AuditEntry(DateTime.UtcNow, AuditActor.Unknown, AuditActions.PatientUpdate, registered.Uuid.ToString(), AuditOutcome.Success);

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(
            new DemographicsUpdateRequest(registered.Uuid, PatientDemographics.Create("Augusta", "King", "Female", _newDob, null, null), [etag], DateTime.UtcNow, audit), CancellationToken.None));

        Assert.Equal(etag, (await ReadAsync(registered.Uuid)).ETag);
        Assert.NotEqual($"King{_newDob:yyyyMMdd}", await Scalar<string>($"SELECT lname FROM patient_data WHERE uuid = UNHEX('{registered.Uuid:N}')"));
    }

    // The duplicate score

    [Fact]
    public async Task A_correction_that_makes_a_patient_match_another_raises_their_duplicate_score()
    {
        var shared = UniqueDob();
        var other = await RegisterAsync(last: $"Twin{Unique()}", sex: "Male", dob: shared, email: null, trusted: null);
        var mover = await RegisterAsync(last: $"Other{Unique()}", sex: "Female", dob: UniqueDob(), email: null, trusted: null);
        var otherLast = await Scalar<string>($"SELECT lname FROM patient_data WHERE uuid = UNHEX('{other.Uuid:N}')");
        var before = await Scalar<long>($"SELECT dupscore FROM patient_data WHERE uuid = UNHEX('{mover.Uuid:N}')");
        var (_, etag) = await ReadAsync(mover.Uuid);

        var response = await PutAsync(mover.Uuid, new UpdatePatientRequest("Ada", otherLast!, "Male", shared, null, null), etag);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await Scalar<long>($"SELECT dupscore FROM patient_data WHERE uuid = UNHEX('{mover.Uuid:N}')") >= 18, $"was {before}");   // name, date of birth and sex now all agree
    }

    // Access

    [Fact]
    public async Task Without_the_gateway_secret_or_a_verified_user_a_correction_is_401_and_changes_nothing()
    {
        var registered = await RegisterAsync();
        var (_, etag) = await ReadAsync(registered.Uuid);
        using var anonymous = _factory!.CreateClient();
        using var noUser = _factory.CreateClient();
        noUser.DefaultRequestHeaders.Add("X-Internal-Gateway-Secret", "test-secret");

        Assert.Equal(HttpStatusCode.Unauthorized, (await PutAsync(registered.Uuid, Change(), etag, anonymous)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutAsync(registered.Uuid, Change(), etag, noUser)).StatusCode);
        Assert.Equal(etag, (await ReadAsync(registered.Uuid)).ETag);
    }
}
