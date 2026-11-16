using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using OpenEmrNext.ClinicalCare.Api.Authentication;
using OpenEmrNext.ClinicalCare.Api.Contracts.Requests;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Api.Endpoints;

/// <summary>
/// The delivery endpoint events are pushed to (ADR 0066). **Not part of the public API**: it is hidden from the OpenAPI
/// document and the generated client, it has no gateway route, and it accepts only the producers' own secret, never the
/// gateway's. Delivery is at-least-once, so everything here is safe to repeat (EVT-3).
/// </summary>
public static class InternalEventEndpoints
{
    private const string PatientRegistered = "PatientRegistered";
    private const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapInternalEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/events", Receive)
            .WithName("ReceiveEvent")
            .ExcludeFromDescription()
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = InternalEventsAuthenticationHandler.SchemeName });

        return app;
    }

    private static async Task<IResult> Receive(
        IncomingEventRequest request, ConsumePatientRegisteredUseCase patientRegistered, CancellationToken ct)
    {
        // An event type this consumer has no interest in is acknowledged and ignored, not rejected: a producer must be
        // free to add new event types without every consumer failing and the producer's relay retrying forever.
        if (!string.Equals(request.EventType, PatientRegistered, StringComparison.Ordinal))
            return Results.Accepted();

        if (request.SchemaVersion != SupportedSchemaVersion)
            return ValidationProblems.Single(
                $"{PatientRegistered} schemaVersion {request.SchemaVersion} is not supported; this service reads version {SupportedSchemaVersion}.");

        // A missing payload property leaves an Undefined JsonElement, which throws (not a JsonException) if deserialized: that was a
        // 500 (review finding R-4), and a 500 is "retry me", for an event that can never succeed.
        if (request.Payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return ValidationProblems.Single($"The {PatientRegistered} event has no payload.");

        PatientRegisteredPayload? payload;
        try
        {
            payload = request.Payload.Deserialize<PatientRegisteredPayload>(PayloadOptions);
        }
        catch (JsonException)
        {
            return ValidationProblems.Single($"The {PatientRegistered} payload is not valid.");
        }

        if (payload is null)
            return ValidationProblems.Single($"The {PatientRegistered} payload is missing.");

        try
        {
            await patientRegistered.ExecuteAsync(
                new PatientRegisteredMessage(request.EventId, payload.Uuid, payload.Pid, payload.RegisteredAtUtc), ct);
        }
        catch (EncounterValidationException ex)
        {
            return ValidationProblems.Many(ex.Errors);
        }

        // Recorded and already-processed are the same answer to the sender: it is done, stop retrying (EVT-3).
        return Results.NoContent();
    }
}
