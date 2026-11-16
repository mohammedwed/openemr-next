using System.Text.Json;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.UnitTests.Persistence;

/// <summary>The outbox payload is a public contract (EVT-2, EVT-5, ARC-7): its field names are pinned by test.</summary>
public class EventEnvelopeTests
{
    private static JsonElement Serialize(EncounterCreatedEvent @event, string? actor = "user:7")
    {
        var json = EventEnvelope.Serialize(EncounterCreatedEvent.EventType, @event.CreatedAtUtc, @event.EncounterUuid.ToString(), actor, @event);
        return JsonDocument.Parse(json).RootElement;
    }

    private static readonly EncounterCreatedEvent Event = new(
        Guid.Parse("c0ffee00-0000-4000-8000-000000000001"), 1001,
        Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa"), 42, "AMB",
        new DateTime(2026, 11, 16, 9, 30, 0), new DateTime(2026, 11, 16, 9, 30, 0, DateTimeKind.Utc));

    [Fact]
    public void The_envelope_carries_exactly_the_contract_fields()
    {
        var names = Serialize(Event).EnumerateObject().Select(p => p.Name).Order().ToArray();

        Assert.Equal(
            ["actor", "causationId", "correlationId", "eventId", "eventType", "occurredAt", "payload", "schemaVersion", "subject"],
            names);
    }

    [Fact]
    public void The_payload_field_names_are_camelCase_and_pinned()
    {
        var names = Serialize(Event).GetProperty("payload").EnumerateObject().Select(p => p.Name).Order().ToArray();

        Assert.Equal(
            ["classCode", "createdAtUtc", "encounterDate", "encounterNumber", "encounterUuid", "patientPid", "patientUuid"],
            names);
    }

    [Fact]
    public void Schema_version_is_one_and_the_event_type_is_the_past_tense_name()
    {
        var root = Serialize(Event);

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("EncounterCreated", root.GetProperty("eventType").GetString());
    }

    [Fact]
    public void Each_envelope_gets_its_own_event_id_so_consumers_can_dedupe_on_it()
    {
        var first = Serialize(Event).GetProperty("eventId").GetGuid();
        var second = Serialize(Event).GetProperty("eventId").GetGuid();

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void The_payload_carries_no_free_text_clinical_content()
    {
        var payload = Serialize(Event).GetProperty("payload").GetRawText();

        Assert.DoesNotContain("reason", payload, StringComparison.OrdinalIgnoreCase);
    }
}

public class VitalsEventEnvelopeTests
{
    [Fact]
    public void VitalsRecorded_payload_names_are_camelCase_pinned_and_hold_no_values()
    {
        var @event = new VitalsRecordedEvent(
            Guid.NewGuid(), Guid.NewGuid(), 1001, Guid.NewGuid(), 42, ["weight", "pulse"], new DateTime(2026, 11, 16, 9, 30, 0, DateTimeKind.Utc));

        var json = EventEnvelope.Serialize(VitalsRecordedEvent.EventType, @event.RecordedAtUtc, @event.VitalsUuid.ToString(), "user:7", @event);
        var root = JsonDocument.Parse(json).RootElement;
        var payload = root.GetProperty("payload");

        Assert.Equal("VitalsRecorded", root.GetProperty("eventType").GetString());
        Assert.Equal(
            ["encounterNumber", "encounterUuid", "measurementsTaken", "patientPid", "patientUuid", "recordedAtUtc", "vitalsUuid"],
            payload.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(["weight", "pulse"], payload.GetProperty("measurementsTaken").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }
}
