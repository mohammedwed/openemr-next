using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.Immunizations;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Immunizations;

/// <summary>The record, correct, get and list use cases against in-memory fakes of every port: orchestration only, no database (DOM-19).</summary>
public class ImmunizationUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly Guid PatientUuid = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    private static readonly Guid EncounterUuid = Guid.Parse("e0e0e0e0-0000-4000-8000-000000000001");
    private static readonly Guid ImmunizationUuid = Guid.Parse("a0a0a0a0-0000-4000-8000-000000000001");
    private static readonly PatientReference Patient = PatientReference.Create(PatientUuid, 42);
    private static readonly DateTime Given = new(2026, 11, 10, 8, 15, 30);

    private static readonly ImmunizationCodeData Codes = new(
        new HashSet<int> { 6, 7 }, new HashSet<string> { "intramuscular" }, new HashSet<string> { "left_arm" }, new HashSet<string> { "Completed" },
        new HashSet<string>(), new HashSet<string>(), new HashSet<string> { "1" });

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class FakePatients(PatientReference? patient) : IPatientDirectory
    {
        public Task<PatientReference?> FindAsync(Guid patientUuid, CancellationToken ct) => Task.FromResult(patient is not null && patient.Uuid == patientUuid ? patient : null);
    }

    private sealed class FakeEncounters(EncounterView? view) : IEncounterReader
    {
        public Task<EncounterView?> FindAsync(Guid encounterUuid, CancellationToken ct) => Task.FromResult(view is not null && view.Uuid == encounterUuid ? view : null);
        public Task<EncounterPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeCodes : IImmunizationCodeLists
    {
        public Task<ImmunizationCodeData> GetCodesAsync(CancellationToken ct) => Task.FromResult(Codes);
    }

    private sealed class FakeIdentities : IImmunizationIdentityAllocator
    {
        public ImmunizationIdentity Allocate() => ImmunizationIdentity.Create(ImmunizationUuid);
    }

    private sealed class FakeRegistry : IImmunizationRegistry
    {
        public Immunization? Registered { get; private set; }
        public ImmunizationRecordedEvent? Event { get; private set; }
        public AuditEntry? Audit { get; private set; }

        public Task RegisterAsync(Immunization immunization, ImmunizationRecordedEvent @event, AuditEntry audit, CancellationToken ct)
        {
            (Registered, Event, Audit) = (immunization, @event, audit);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReader(ImmunizationView? view) : IImmunizationReader
    {
        public ImmunizationView? View { get; set; } = view;

        public Task<ImmunizationView?> FindAsync(Guid immunizationUuid, CancellationToken ct) =>
            Task.FromResult(View is not null && View.Uuid == immunizationUuid ? View : null);

        public Task<ImmunizationPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct) =>
            Task.FromResult(new ImmunizationPage(View is null ? [] : [View], View is null ? 0 : 1));
    }

    private sealed class FakeUpdater(FakeReader reader) : IImmunizationUpdater
    {
        public ImmunizationUpdateOutcome? Outcome { get; set; }
        public ImmunizationUpdateRequest? Request { get; private set; }

        public Task<ImmunizationUpdateOutcome> UpdateAsync(ImmunizationUpdateRequest request, CancellationToken ct)
        {
            Request = request;
            var outcome = Outcome ?? new ImmunizationUpdateOutcome(UpdateStatus.Updated, null);
            if (outcome.Status == UpdateStatus.Updated && reader.View is { } v)
            {
                var f = request.Facts;
                reader.View = v with
                {
                    AdministeredDate = f.AdministeredDate, ImmunizationId = f.ImmunizationId, CvxCode = f.CvxCode, Manufacturer = f.Manufacturer, LotNumber = f.LotNumber,
                    ExpirationDate = f.ExpirationDate, AmountAdministered = f.AmountAdministered, AmountUnit = f.AmountUnit, Route = f.Route,
                    AdministrationSite = f.AdministrationSite, CompletionStatus = f.CompletionStatus, InformationSource = f.InformationSource,
                    RefusalReason = f.RefusalReason, EducationDate = f.EducationDate, VisDate = f.VisDate, AdministeredBy = f.AdministeredBy,
                    AdministeredById = f.AdministeredById, OrderingProvider = f.OrderingProvider, EncounterNumber = f.EncounterNumber, ReasonCode = f.ReasonCode,
                    ReasonDescription = f.ReasonDescription, Note = f.Note, EnteredInError = f.EnteredInError
                };
            }

            return Task.FromResult(outcome);
        }
    }

    private sealed class FakeAuditLog : IAuditLog
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task AppendAsync(AuditEntry entry, CancellationToken ct)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private static EncounterView Encounter(long? number = 1001, long? pid = 42) =>
        new(EncounterUuid, number, PatientUuid, pid, "AMB", null, null, null, 0, null, null, 7, null);

    private static ImmunizationView Stored() => new(
        ImmunizationUuid, PatientUuid, 42, Given, 6, "140", "Acme", "L1", null, null, null, "intramuscular", "left_arm", "Completed", null, null, null, null, "Joy", 7, null,
        null, null, null, "arm sore", false, new DateTime(2026, 11, 10, 8, 20, 0));

    private static ImmunizationInput Same() => new(Given, 6, "140", "Acme", "L1", Route: "intramuscular", AdministrationSite: "left_arm", CompletionStatus: "Completed",
        AdministeredBy: "Joy", AdministeredById: 7, Note: "arm sore");

    // Record

    private static (RecordImmunizationUseCase UseCase, FakeRegistry Registry, FakeAuditLog Log) BuildRecord(PatientReference? patient, EncounterView? encounter = null)
    {
        var registry = new FakeRegistry();
        var log = new FakeAuditLog();
        return (new RecordImmunizationUseCase(new FakePatients(patient), new FakeEncounters(encounter), new FakeCodes(), new FakeIdentities(), registry, log, new FixedClock(Now)),
            registry, log);
    }

    [Fact]
    public async Task A_valid_immunization_is_registered_with_its_event_and_audit_row_naming_ids_only()
    {
        var (useCase, registry, log) = BuildRecord(Patient);

        var result = await useCase.ExecuteAsync(
            new RecordImmunizationCommand(PatientUuid, Same() with { Note = "secret detail" }, null, 7, new RequestOrigin("trace-i", "192.0.2.9")), CancellationToken.None);

        Assert.Equal(ImmunizationUuid, result.Uuid);
        Assert.Same(result, registry.Registered);
        Assert.Equal(new ImmunizationRecordedEvent(ImmunizationUuid, PatientUuid, 42, Now.UtcDateTime), registry.Event);
        var audit = registry.Audit!;
        Assert.Equal(AuditActions.ImmunizationRecord, audit.Action);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal(ImmunizationUuid, audit.Subject);
        Assert.Equal("user:7", audit.Actor.Subject);
        Assert.Equal("trace-i", audit.CorrelationId);
        Assert.Null(audit.Details);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task A_linked_encounter_is_resolved_to_its_number_and_must_be_the_patients()
    {
        var (useCase, registry, _) = BuildRecord(Patient, Encounter());

        await useCase.ExecuteAsync(new RecordImmunizationCommand(PatientUuid, Same(), EncounterUuid, 7), CancellationToken.None);
        Assert.Equal(1001, registry.Registered!.Facts.EncounterNumber);

        var other = BuildRecord(Patient, Encounter(pid: 99));
        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(
            () => other.UseCase.ExecuteAsync(new RecordImmunizationCommand(PatientUuid, Same(), EncounterUuid, 7), CancellationToken.None));
        Assert.Contains("The encounter belongs to a different patient than the immunization.", ex.Errors);
        Assert.Null(other.Registry.Registered);
    }

    [Fact]
    public async Task An_unknown_patient_or_encounter_is_not_found_nothing_is_registered_and_the_refusal_is_audited()
    {
        var noPatient = BuildRecord(null);
        var noEncounter = BuildRecord(Patient, null);

        await Assert.ThrowsAsync<PatientNotFoundException>(() => noPatient.UseCase.ExecuteAsync(new RecordImmunizationCommand(PatientUuid, Same(), null, 7), CancellationToken.None));
        await Assert.ThrowsAsync<EncounterNotFoundException>(
            () => noEncounter.UseCase.ExecuteAsync(new RecordImmunizationCommand(PatientUuid, Same(), EncounterUuid, 7), CancellationToken.None));

        Assert.Null(noPatient.Registry.Registered);
        Assert.Null(noEncounter.Registry.Registered);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(noPatient.Log.Entries).Outcome);
        Assert.Equal(PatientUuid, noPatient.Log.Entries[0].Subject);
        Assert.Equal(AuditOutcome.Failed, Assert.Single(noEncounter.Log.Entries).Outcome);
    }

    [Fact]
    public async Task A_validation_failure_registers_nothing_and_is_audited_as_failed_without_values()
    {
        var (useCase, registry, log) = BuildRecord(Patient);

        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(
            () => useCase.ExecuteAsync(new RecordImmunizationCommand(PatientUuid, Same() with { ImmunizationId = 99 }, null, 7), CancellationToken.None));

        Assert.Contains("Immunization 99 is not a known immunization.", ex.Errors);
        Assert.Null(registry.Registered);
        var row = Assert.Single(log.Entries);
        Assert.Equal(AuditOutcome.Failed, row.Outcome);
        Assert.Null(row.Details);
    }

    [Fact]
    public async Task A_provider_id_the_column_cannot_hold_is_a_validation_failure_not_a_database_error()
    {
        var (useCase, registry, _) = BuildRecord(Patient);

        await Assert.ThrowsAsync<ClinicalCareValidationException>(
            () => useCase.ExecuteAsync(new RecordImmunizationCommand(PatientUuid, Same(), null, 5_000_000_000), CancellationToken.None));

        Assert.Null(registry.Registered);
    }

    // Correct

    private static (CorrectImmunizationUseCase UseCase, FakeReader Reader, FakeUpdater Updater, FakeAuditLog Log) BuildCorrect(ImmunizationView? stored, EncounterView? encounter = null)
    {
        var reader = new FakeReader(stored);
        var updater = new FakeUpdater(reader);
        var log = new FakeAuditLog();
        return (new CorrectImmunizationUseCase(reader, new FakeEncounters(encounter), new FakeCodes(), updater, log, new FixedClock(Now)), reader, updater, log);
    }

    private static CorrectImmunizationCommand Correct(ImmunizationInput input, IReadOnlyList<ContentVersion>? offered = null, Guid? encounter = null, RequestOrigin? origin = null) =>
        new(ImmunizationUuid, offered ?? [Stored().Version()], input, encounter, origin);

    [Fact]
    public async Task A_correction_is_written_with_the_version_it_was_based_on_and_names_the_changed_fields_only()
    {
        var (useCase, _, updater, log) = BuildCorrect(Stored());

        var result = await useCase.ExecuteAsync(Correct(Same() with { LotNumber = "L2", Note = "rechecked" }, origin: new RequestOrigin("trace-c", null)), CancellationToken.None);

        Assert.Equal(["lotNumber", "note"], result.ChangedFields);
        Assert.Equal("L2", result.Immunization.LotNumber);
        var request = updater.Request!;
        Assert.Equal(Stored().Version(), request.Expected);
        Assert.Equal(42, request.PatientPid);
        Assert.Equal(["lotNumber", "note"], request.Event.ChangedFields);
        Assert.Equal(Now.UtcDateTime, request.Event.CorrectedAtUtc);
        Assert.Equal(AuditActions.ImmunizationCorrect, request.Audit.Action);
        Assert.Equal(AuditActor.Unknown, request.Audit.Actor);
        Assert.Equal("""["lotNumber","note"]""", request.Audit.Details);
        Assert.DoesNotContain("rechecked", request.Audit.Details);
        Assert.Equal("trace-c", request.Audit.CorrelationId);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task Resubmitting_what_is_stored_writes_nothing_and_audits_nothing()
    {
        var (useCase, _, updater, log) = BuildCorrect(Stored());

        var result = await useCase.ExecuteAsync(Correct(Same()), CancellationToken.None);

        Assert.Empty(result.ChangedFields);
        Assert.Null(updater.Request);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public async Task Marking_an_entry_in_error_is_a_correction_like_any_other()
    {
        var (useCase, _, updater, _) = BuildCorrect(Stored());

        var result = await useCase.ExecuteAsync(Correct(Same() with { EnteredInError = true }), CancellationToken.None);

        Assert.Equal(["enteredInError"], result.ChangedFields);
        Assert.True(updater.Request!.Facts.EnteredInError);
    }

    [Fact]
    public async Task A_stale_or_missing_version_is_refused_before_anything_is_validated_and_carries_the_current_version()
    {
        var (useCase, _, updater, log) = BuildCorrect(Stored());
        var stale = ContentVersion.Of(["stale"]);

        var ex = await Assert.ThrowsAsync<ImmunizationConflictException>(
            () => useCase.ExecuteAsync(Correct(Same() with { ImmunizationId = 99 }, offered: [stale]), CancellationToken.None));   // also invalid
        await Assert.ThrowsAsync<ImmunizationConflictException>(() => useCase.ExecuteAsync(Correct(Same(), offered: []), CancellationToken.None));

        Assert.Equal(Stored().Version(), ex.CurrentVersion);
        Assert.Null(updater.Request);
        Assert.All(log.Entries, e => Assert.Equal(AuditOutcome.Failed, e.Outcome));
        Assert.Equal(2, log.Entries.Count);
    }

    [Fact]
    public async Task An_unknown_immunization_is_not_found_and_a_row_with_no_known_patient_cannot_be_corrected()
    {
        var none = BuildCorrect(null);
        var orphan = BuildCorrect(Stored() with { PatientPid = null });

        await Assert.ThrowsAsync<ImmunizationNotFoundException>(() => none.UseCase.ExecuteAsync(Correct(Same()), CancellationToken.None));
        await Assert.ThrowsAsync<ImmunizationNotFoundException>(
            () => orphan.UseCase.ExecuteAsync(Correct(Same() with { Note = "x" }, offered: [(Stored() with { PatientPid = null }).Version()]), CancellationToken.None));

        Assert.Equal(AuditOutcome.Failed, Assert.Single(none.Log.Entries).Outcome);
        Assert.Null(orphan.Updater.Request);
    }

    [Fact]
    public async Task A_correction_that_links_another_patients_encounter_is_refused()
    {
        var (useCase, _, updater, _) = BuildCorrect(Stored(), Encounter(pid: 99));

        var ex = await Assert.ThrowsAsync<ClinicalCareValidationException>(() => useCase.ExecuteAsync(Correct(Same(), encounter: EncounterUuid), CancellationToken.None));

        Assert.Contains("The encounter belongs to a different patient than the immunization.", ex.Errors);
        Assert.Null(updater.Request);
    }

    [Fact]
    public async Task A_conflict_found_at_write_time_and_a_vanished_row_are_reported_as_such()
    {
        var (useCase, _, updater, log) = BuildCorrect(Stored());
        var winner = ContentVersion.Of(["winner"]);
        updater.Outcome = new ImmunizationUpdateOutcome(UpdateStatus.Conflict, winner);

        var ex = await Assert.ThrowsAsync<ImmunizationConflictException>(() => useCase.ExecuteAsync(Correct(Same() with { Note = "x" }), CancellationToken.None));
        updater.Outcome = new ImmunizationUpdateOutcome(UpdateStatus.NotFound, null);
        await Assert.ThrowsAsync<ImmunizationNotFoundException>(() => useCase.ExecuteAsync(Correct(Same() with { Note = "x" }), CancellationToken.None));

        Assert.Equal(winner, ex.CurrentVersion);
        Assert.Equal(2, log.Entries.Count);
    }

    // Reads

    [Fact]
    public async Task Get_returns_the_view_or_nothing_and_list_needs_a_registered_patient_and_valid_bounds()
    {
        var reader = new FakeReader(Stored());

        Assert.NotNull(await new GetImmunizationUseCase(reader).ExecuteAsync(ImmunizationUuid, CancellationToken.None));
        Assert.Null(await new GetImmunizationUseCase(reader).ExecuteAsync(Guid.NewGuid(), CancellationToken.None));

        var list = new ListPatientImmunizationsUseCase(new FakePatients(Patient), reader);
        Assert.Equal(1, (await list.ExecuteAsync(PatientUuid, null, null, CancellationToken.None)).Total);
        await Assert.ThrowsAsync<PatientNotFoundException>(() => list.ExecuteAsync(Guid.NewGuid(), null, null, CancellationToken.None));
        await Assert.ThrowsAsync<ClinicalCareValidationException>(() => list.ExecuteAsync(PatientUuid, 100_000, null, CancellationToken.None));
    }
}
