using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Api.Contracts.Responses;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// <c>POST /internal/events</c> (ADR 0066): delivery is at-least-once, authenticated by the producers' own secret and no
/// other, and the effect is that Clinical Care learns of a patient and can then create encounters for them.
/// </summary>
[Collection("ClinicalCareContainer")]
public class EventDeliveryEndpointTests(ClinicalCareContainerFixture fixture) : IAsyncLifetime
{
    private static readonly Guid PatientUuid = Guid.Parse("e1e1e1e1-0000-4000-8000-0000000000aa");

    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        _factory = ApiFactory.Create(fixture);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private HttpClient Client(string? header = ApiFactory.EventsSecretHeader, string? secret = ApiFactory.EventsSecret)
    {
        var client = _factory.CreateClient();
        if (header is not null && secret is not null)
            client.DefaultRequestHeaders.Add(header, secret);
        return client;
    }

    // The shape the PatientManagement relay sends (camelCase, enveloped), including fields this consumer ignores.
    private static StringContent Envelope(Guid eventId, string eventType = "PatientRegistered", int schemaVersion = 1,
        Guid? uuid = null, long pid = 42, string? payloadOverride = null) =>
        new($$"""
        {
          "eventId": "{{eventId}}", "eventType": "{{eventType}}", "schemaVersion": {{schemaVersion}},
          "occurredAt": "2026-11-16T09:00:00Z", "subject": "{{uuid ?? PatientUuid}}", "actor": null,
          "correlationId": "abc", "causationId": null, "futureEnvelopeField": 1,
          "payload": {{payloadOverride ?? $$"""{ "uuid": "{{uuid ?? PatientUuid}}", "pid": {{pid}}, "firstName": "Katherine", "lastName": "Johnson", "registeredAtUtc": "2026-11-16T09:00:00Z" }"""}}
        }
        """, Encoding.UTF8, "application/json");

    [Fact]
    public async Task A_PatientRegistered_event_makes_the_patient_known_and_an_encounter_can_then_be_created()
    {
        // Before: Clinical Care has never heard of this patient, so an encounter for them is a 404.
        using var gateway = _factory.ClientWithSecret();
        var request = new CreateEncounterRequest(PatientUuid, null, 5, null, "visit", 3, null, 7, null, null);
        Assert.Equal(HttpStatusCode.NotFound, (await gateway.PostAsJsonAsync("/api/v1/encounters", request)).StatusCode);

        using var producer = Client();
        var delivered = await producer.PostAsync("/internal/events", Envelope(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NoContent, delivered.StatusCode);
        // After: the same request now succeeds, because the event is the only thing that changed.
        var created = await gateway.PostAsJsonAsync("/api/v1/encounters", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(PatientUuid, (await created.Content.ReadFromJsonAsync<EncounterCreatedResponse>())!.PatientUuid);
    }

    [Fact]
    public async Task EVT_3_the_same_event_delivered_twice_is_applied_once_and_both_deliveries_succeed()
    {
        using var producer = Client();
        var eventId = Guid.NewGuid();

        var first = await producer.PostAsync("/internal/events", Envelope(eventId));
        var second = await producer.PostAsync("/internal/events", Envelope(eventId));

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_processed_events"));
    }

    [Fact]
    public async Task Unknown_envelope_fields_are_ignored_a_tolerant_reader()
    {
        using var producer = Client();

        // Envelope() already sends "futureEnvelopeField" and names this consumer does not use.
        Assert.Equal(HttpStatusCode.NoContent, (await producer.PostAsync("/internal/events", Envelope(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task An_event_type_this_consumer_does_not_handle_is_acknowledged_and_ignored_not_rejected()
    {
        using var producer = Client();

        var response = await producer.PostAsync("/internal/events", Envelope(Guid.NewGuid(), eventType: "SomethingNew"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_processed_events"));
    }

    [Fact]
    public async Task An_unsupported_schema_version_is_a_400_so_the_producer_can_alert_not_a_silent_skip()
    {
        using var producer = Client();

        var response = await producer.PostAsync("/internal/events", Envelope(Guid.NewGuid(), schemaVersion: 2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
    }

    [Theory]
    [InlineData("""{ "uuid": "00000000-0000-0000-0000-000000000000", "pid": 42 }""")]
    [InlineData("""{ "uuid": "e1e1e1e1-0000-4000-8000-0000000000aa", "pid": 0 }""")]
    [InlineData("""{ "uuid": "e1e1e1e1-0000-4000-8000-0000000000aa", "pid": -3 }""")]
    [InlineData("""{ "uuid": "not-a-guid", "pid": 42 }""")]
    [InlineData("null")]
    public async Task An_unusable_patient_payload_is_a_400_and_nothing_is_recorded(string payload)
    {
        using var producer = Client();

        var response = await producer.PostAsync("/internal/events", Envelope(Guid.NewGuid(), payloadOverride: payload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_processed_events"));
    }

    [Fact]
    public async Task An_event_with_no_event_id_cannot_be_processed_safely_and_is_a_400()
    {
        using var producer = Client();

        var response = await producer.PostAsync("/internal/events", Envelope(Guid.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SEC_18_delivery_with_no_secret_a_wrong_secret_or_the_gateways_secret_is_401()
    {
        using var none = Client(header: null);
        using var wrong = Client(secret: "not-the-secret");
        using var gatewaysSecret = Client(ApiFactory.SecretHeader, ApiFactory.Secret);
        using var gatewaysSecretUnderTheEventsHeader = Client(ApiFactory.EventsSecretHeader, ApiFactory.Secret);

        foreach (var client in new[] { none, wrong, gatewaysSecret, gatewaysSecretUnderTheEventsHeader })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/internal/events", Envelope(Guid.NewGuid()))).StatusCode);

        Assert.Equal(0L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
    }

    [Fact]
    public async Task API_16_the_producers_secret_cannot_call_the_public_api()
    {
        using var producer = Client();
        var request = new CreateEncounterRequest(PatientUuid, null, 5, null, "visit", 3, null, 7, null, null);

        Assert.Equal(HttpStatusCode.Unauthorized, (await producer.PostAsJsonAsync("/api/v1/encounters", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await producer.GetAsync($"/api/v1/encounters?patientUuid={PatientUuid}")).StatusCode);
    }

    [Fact]
    public async Task The_events_endpoint_is_not_in_the_public_openapi_document()
    {
        using var development = ApiFactory.Create(fixture, builder => builder.UseEnvironment("Development"));
        using var client = development.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.DoesNotContain("/internal/events", document);
    }

    [Fact]
    public async Task R_9_an_event_that_contradicts_a_held_patient_is_a_422_the_relay_will_dead_letter_not_a_500_it_would_retry()
    {
        await fixture.SeedPatientAsync(Guid.Parse("a0a0a0a0-0000-4000-8000-000000000001"), 42);
        using var producer = Client();
        var conflicting = Guid.NewGuid();

        // A different patient (PatientUuid) claiming pid 42, which is already held.
        var response = await producer.PostAsync("/internal/events", Envelope(conflicting, pid: 42));

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0L, await fixture.RootScalarAsync<long>($"SELECT COUNT(*) FROM clinical_care_processed_events WHERE event_id = '{conflicting}'"));
        Assert.Equal(1L, await fixture.RootScalarAsync<long>("SELECT COUNT(*) FROM clinical_care_patient_ref"));
    }
}
