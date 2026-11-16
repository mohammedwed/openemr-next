using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// The consumer's persistence (EVT-3) against a real MariaDB, as the production principal: an event is applied once, a
/// redelivery changes nothing, and a failure leaves no half-applied event.
/// </summary>
[Collection("ClinicalCareContainer")]
public class PatientReferenceStoreTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid Uuid1 = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    private static readonly Guid Uuid2 = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000bb");

    public Task InitializeAsync() => fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<EventRecordingResult> RecordAsync(Guid eventId, Guid uuid, long pid)
    {
        await using var db = fixture.CreateDbContext();
        return await new EfPatientReferenceStore(db).RecordPatientRegisteredAsync(
            eventId, PatientReference.Create(uuid, pid), new DateTime(2026, 11, 16, 9, 0, 0), CancellationToken.None);
    }

    [Fact]
    public async Task A_new_event_records_the_patient_reference_and_marks_the_event_processed()
    {
        var eventId = Guid.NewGuid();

        var result = await RecordAsync(eventId, Uuid1, 42);

        Assert.Equal(EventRecordingResult.Recorded, result);
        Assert.Equal(42L, await fixture.RootScalarAsync<long>($"SELECT pid FROM clinical_care_patient_ref WHERE HEX(patient_uuid) = '{Uuid1.ToString("N").ToUpperInvariant()}'"));
        Assert.Equal("event", await fixture.RootScalarAsync<string>("SELECT source FROM clinical_care_patient_ref"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM clinical_care_processed_events WHERE event_id = '{eventId}'"));
    }

    [Fact]
    public async Task EVT_3_redelivering_the_same_event_changes_nothing()
    {
        var eventId = Guid.NewGuid();
        await RecordAsync(eventId, Uuid1, 42);

        var again = await RecordAsync(eventId, Uuid1, 42);

        Assert.Equal(EventRecordingResult.AlreadyProcessed, again);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_processed_events"));
    }

    [Fact]
    public async Task A_second_event_for_an_already_known_patient_is_recorded_without_a_duplicate_reference()
    {
        await fixture.SeedPatientAsync(Uuid1, 42); // the backfill got there first
        var eventId = Guid.NewGuid();

        var result = await RecordAsync(eventId, Uuid1, 42);

        Assert.Equal(EventRecordingResult.Recorded, result);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_processed_events"));
    }

    [Fact]
    public async Task A_different_patient_claiming_a_held_pid_fails_and_leaves_no_event_marked_processed()
    {
        await RecordAsync(Guid.NewGuid(), Uuid1, 42);
        var conflicting = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<Exception>(() => RecordAsync(conflicting, Uuid2, 42));

        // The failed event is NOT recorded as processed, so a redelivery after the conflict is resolved will be applied.
        Assert.Equal(0L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM clinical_care_processed_events WHERE event_id = '{conflicting}'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
    }

    [Fact]
    public async Task Concurrent_deliveries_of_one_event_apply_it_exactly_once()
    {
        var eventId = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => RecordAsync(eventId, Uuid1, 42)));

        Assert.Equal(1, results.Count(r => r == EventRecordingResult.Recorded));
        Assert.Equal(7, results.Count(r => r == EventRecordingResult.AlreadyProcessed));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
    }
}
