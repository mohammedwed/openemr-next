using System.Text.Json;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Api.Contracts.Responses;
using OpenEmrNext.PatientManagement.Api.Domain;
using OpenEmrNext.PatientManagement.Api.Events;
using OpenEmrNext.PatientManagement.Api.Persistence;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>Endpoints for registering patients.</summary>
public static class PatientEndpoints
{
    public static IEndpointRouteBuilder MapPatientEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/patients", RegisterPatient)
            .WithName("RegisterPatient")
            .WithTags("Patients")
            .WithSummary("Register a new patient")
            .Produces<PatientRegisteredResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> RegisterPatient(
        RegisterPatientRequest request, PatientManagementDbContext db, CancellationToken ct)
    {
        Patient patient;
        try
        {
            patient = Patient.Register(
                new PatientRegistration(
                    request.FirstName, request.LastName, request.Sex, request.DateOfBirth,
                    request.Email, request.TrustedEmail),
                request.RegisteredBy,
                DateTime.UtcNow);
        }
        catch (PatientValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["errors"] = [.. ex.Errors] });
        }

        var @event = new PatientRegisteredEvent(
            patient.Uuid, patient.Pid, patient.FirstName, patient.LastName, patient.RegisteredAtUtc);

        // One SaveChangesAsync across both tables is the whole point (ADR 0049): the patient row and
        // the fact that PatientRegistered needs publishing commit together, or neither does.
        db.Patients.Add(patient);
        db.OutboxMessages.Add(OutboxMessage.For(
            PatientRegisteredEvent.EventType, JsonSerializer.Serialize(@event), DateTime.UtcNow));
        await db.SaveChangesAsync(ct);

        return Results.Created(
            $"/api/v1/patients/{patient.Uuid}",
            new PatientRegisteredResponse(patient.Uuid, patient.Pid, patient.Pubpid));
    }
}
