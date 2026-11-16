using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.UnitTests.Application;

public class ConsumePatientRegisteredUseCaseTests
{
    private sealed class FakeStore(EventRecordingResult result) : IPatientReferenceStore
    {
        public int Calls { get; private set; }
        public Guid? EventId { get; private set; }
        public PatientReference? Patient { get; private set; }

        public Task<EventRecordingResult> RecordPatientRegisteredAsync(
            Guid eventId, PatientReference patient, DateTime? registeredAtUtc, CancellationToken ct)
        {
            Calls++;
            EventId = eventId;
            Patient = patient;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task A_valid_message_is_handed_to_the_store_with_the_event_id_and_the_patient_reference()
    {
        var store = new FakeStore(EventRecordingResult.Recorded);
        var eventId = Guid.NewGuid();
        var patient = Guid.NewGuid();

        var result = await new ConsumePatientRegisteredUseCase(store)
            .ExecuteAsync(new PatientRegisteredMessage(eventId, patient, 42, null), CancellationToken.None);

        Assert.Equal(EventRecordingResult.Recorded, result);
        Assert.Equal(eventId, store.EventId);
        Assert.Equal(patient, store.Patient!.Uuid);
        Assert.Equal(42, store.Patient.Pid);
    }

    [Fact]
    public async Task A_redelivery_reported_by_the_store_is_passed_back_unchanged()
    {
        var result = await new ConsumePatientRegisteredUseCase(new FakeStore(EventRecordingResult.AlreadyProcessed))
            .ExecuteAsync(new PatientRegisteredMessage(Guid.NewGuid(), Guid.NewGuid(), 42, null), CancellationToken.None);

        Assert.Equal(EventRecordingResult.AlreadyProcessed, result);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "b1a9c5f0-0000-4000-8000-0000000000aa", 42)]
    [InlineData("c0ffee00-0000-4000-8000-000000000001", "00000000-0000-0000-0000-000000000000", 42)]
    [InlineData("c0ffee00-0000-4000-8000-000000000001", "b1a9c5f0-0000-4000-8000-0000000000aa", 0)]
    public async Task An_unusable_message_is_rejected_before_the_store_is_touched(string eventId, string patient, long pid)
    {
        var store = new FakeStore(EventRecordingResult.Recorded);

        await Assert.ThrowsAsync<EncounterValidationException>(() => new ConsumePatientRegisteredUseCase(store)
            .ExecuteAsync(new PatientRegisteredMessage(Guid.Parse(eventId), Guid.Parse(patient), pid, null), CancellationToken.None));

        Assert.Equal(0, store.Calls);
    }
}
