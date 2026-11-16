using System.Text.Json;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.UnitTests.Relay;

/// <summary>
/// The pure step that turns an outbox row into the event that travels (EVT-2, EVT-3). Rows written before any consumer
/// existed (PascalCase, no envelope) must leave identically to new ones.
/// </summary>
public class OutboundEventFactoryTests
{
    private static readonly Guid Patient = Guid.Parse("b1a9c5f0-0000-4000-8000-0000000000aa");
    private static readonly DateTime Occurred = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc);

    // What EfPatientRegistry wrote before slice 3: default serializer options, so PascalCase.
    private static string Legacy =>
        $$"""{"Uuid":"{{Patient}}","Pid":42,"FirstName":"Katherine","LastName":"Johnson","RegisteredAtUtc":"2026-11-16T09:00:00Z"}""";

    private static OutboxEntry Row(long id = 7, string type = "PatientRegistered", string? payload = null) =>
        new(id, type, payload ?? Legacy, Occurred, 0);

    [Fact]
    public void An_old_pascal_case_row_leaves_with_the_pinned_camel_case_names()
    {
        var evt = OutboundEventFactory.From(Row());

        var names = JsonDocument.Parse(evt.PayloadJson).RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(["firstName", "lastName", "pid", "registeredAtUtc", "uuid"], names);
    }

    [Fact]
    public void A_row_already_in_camel_case_leaves_identically_to_an_old_one()
    {
        var camel = $$"""{"uuid":"{{Patient}}","pid":42,"firstName":"Katherine","lastName":"Johnson","registeredAtUtc":"2026-11-16T09:00:00Z"}""";

        Assert.Equal(OutboundEventFactory.From(Row()).PayloadJson, OutboundEventFactory.From(Row(payload: camel)).PayloadJson);
    }

    [Fact]
    public void The_envelope_carries_type_version_time_and_the_patient_as_subject()
    {
        var evt = OutboundEventFactory.From(Row());

        Assert.Equal("PatientRegistered", evt.EventType);
        Assert.Equal(1, evt.SchemaVersion);
        Assert.Equal(Occurred, evt.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, evt.OccurredAtUtc.Kind);
        Assert.Equal(Patient.ToString(), evt.Subject);
        Assert.False(string.IsNullOrEmpty(evt.CorrelationId));
    }

    [Fact]
    public void EVT_3_the_same_row_always_gets_the_same_event_id_and_different_rows_differ()
    {
        Assert.Equal(OutboundEventFactory.From(Row(7)).EventId, OutboundEventFactory.From(Row(7)).EventId);
        Assert.Equal(OutboundEventFactory.EventIdFor(7), OutboundEventFactory.EventIdFor(7));
        Assert.NotEqual(OutboundEventFactory.EventIdFor(7), OutboundEventFactory.EventIdFor(8));
        Assert.NotEqual(Guid.Empty, OutboundEventFactory.EventIdFor(1));
    }

    [Fact]
    public void The_event_id_is_a_well_formed_version_5_uuid()
    {
        var text = OutboundEventFactory.EventIdFor(123).ToString();

        Assert.Equal('5', text[14]);                       // version nibble
        Assert.Contains(text[19], "89ab");                 // RFC 4122 variant
    }

    [Fact]
    public void An_unknown_event_type_is_forwarded_as_stored_for_the_consumer_to_judge()
    {
        var evt = OutboundEventFactory.From(Row(9, "SomethingNew", """{"Whatever":1}"""));

        Assert.Equal("SomethingNew", evt.EventType);
        Assert.Equal("""{"Whatever":1}""", evt.PayloadJson);
        Assert.Equal("9", evt.Subject);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"Uuid":"00000000-0000-0000-0000-000000000000","Pid":42}""")]
    [InlineData("""{"Uuid":"b1a9c5f0-0000-4000-8000-0000000000aa","Pid":0}""")]
    public void A_payload_that_names_no_usable_patient_can_never_be_published(string payload)
    {
        Assert.Throws<UnpublishableEventException>(() => OutboundEventFactory.From(Row(payload: payload)));
    }
}
