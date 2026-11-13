using OpenEmrNext.PatientManagement.Api.Contracts.Fhir;
using OpenEmrNext.PatientManagement.Api.Contracts.Requests;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.Fhir;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>
/// FHIR R4 Patient endpoints, under <c>/fhir</c> rather than <c>/api/v1</c> -- FHIR resources are not
/// versioned the way this service's own internal API is, matching legacy's own
/// <c>/fhir/Patient</c> route convention. Covers <c>GET</c> (read + search) and <c>POST</c> (create,
/// reusing <see cref="RegisterPatientUseCase"/>, the same path plain registration already uses).
/// <c>PUT</c> (update) is deliberately not built in this slice: it would need a brand-new
/// <c>Patient.Update()</c>/persistence capability that doesn't exist anywhere in this service yet
/// (only <c>RegisterPatientUseCase</c>/<c>IPatientRegistry.RegisterAsync</c> do, both create-only) --
/// not a case of "reusing what exists," despite the parity plan's original assumption that it would
/// be. <c>$export</c>, <c>DELETE</c>, <c>$everything</c>, and <c>$match</c> are out of scope: the first
/// per the plan, the rest because legacy doesn't implement them either.
/// </summary>
public static class FhirPatientEndpoints
{
    public static IEndpointRouteBuilder MapFhirPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var fhir = app.MapGroup("/fhir");

        fhir.MapGet("/Patient/{uuid:guid}", GetPatient)
            .WithName("FhirGetPatient")
            .WithTags("Fhir")
            .WithSummary("Read a patient as a FHIR R4 Patient resource")
            .Produces<FhirPatient>()
            .Produces(StatusCodes.Status404NotFound);

        fhir.MapGet("/Patient", SearchPatients)
            .WithName("FhirSearchPatients")
            .WithTags("Fhir")
            .WithSummary("Search patients, returning a FHIR searchset Bundle")
            .Produces<FhirBundle>();

        fhir.MapPost("/Patient", CreatePatient)
            .WithName("FhirCreatePatient")
            .WithTags("Fhir")
            .WithSummary("Create a patient from a FHIR R4 Patient resource")
            .Produces<FhirPatient>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        return app;
    }

    private static async Task<IResult> GetPatient(Guid uuid, GetPatientFhirRecordUseCase useCase, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(uuid, ct);
        return view is null ? Results.NotFound() : Results.Ok(FhirPatientMapper.ToFhirPatient(view));
    }

    private static async Task<IResult> SearchPatients(
        HttpRequest request, SearchPatientFhirRecordsUseCase useCase, CancellationToken ct)
    {
        var query = request.Query;
        var criteria = new PatientFhirSearchCriteria(
            Id: Guid.TryParse(query["_id"], out var id) ? id : null,
            Identifier: query["identifier"],
            Name: query["name"],
            Family: query["family"],
            Given: query["given"],
            BirthDate: DateOnly.TryParseExact(query["birthdate"], "yyyy-MM-dd", out var dob) ? dob : null,
            Gender: query["gender"],
            Address: query["address"],
            AddressCity: query["address-city"],
            AddressPostalCode: query["address-postalcode"],
            AddressState: query["address-state"],
            Email: query["email"],
            Phone: query["phone"],
            Telecom: query["telecom"],
            LastUpdatedSince: DateTime.TryParse(query["_lastUpdated"], out var since) ? since : null,
            GeneralPractitioner: Guid.TryParse(query["generalPractitioner"], out var gp) ? gp : null);

        var views = await useCase.ExecuteAsync(criteria, ct);
        var entries = views
            .Select(FhirPatientMapper.ToFhirPatient)
            .Select(p => new FhirBundleEntry($"Patient/{p.Id}", p, new FhirBundleSearch("match")))
            .ToList();

        return Results.Ok(new FhirBundle("Bundle", "searchset", entries.Count, entries));
    }

    private static async Task<IResult> CreatePatient(
        CreateFhirPatientRequest request, long registeredBy,
        RegisterPatientUseCase registerUseCase, ScorePatientForDuplicatesUseCase scoreUseCase,
        GetPatientFhirRecordUseCase getUseCase, CancellationToken ct)
    {
        // Ports parseFhirResource()'s subset: the official name (first entry if none is marked
        // "official," matching legacy's own fallback), telecom's email/email_direct (use=mobile ->
        // TrustedEmail, matching legacy's exact use-code mapping; everything else -> Email), gender,
        // and birthDate. PatientRegistration has no middle-name/title/phone fields at all, so a FHIR
        // resource carrying them silently drops them here -- an existing limitation of
        // PatientRegistration itself, not something this endpoint adds.
        var name = request.Name?.FirstOrDefault(n => n.Use == "official") ?? request.Name?.FirstOrDefault();
        var firstName = name?.Given.FirstOrDefault() ?? "";
        var lastName = name?.Family ?? "";

        string? email = null;
        string? trustedEmail = null;
        foreach (var contact in request.Telecom ?? [])
        {
            if (contact.System != "email") continue;
            if (contact.Use == "mobile") trustedEmail = contact.Value;
            else email = contact.Value;
        }

        var dateOfBirth = DateOnly.TryParseExact(request.BirthDate, "yyyy-MM-dd", out var dob) ? dob : default;

        Patient patient;
        try
        {
            patient = await registerUseCase.ExecuteAsync(
                new PatientRegistration(firstName, lastName, request.Gender ?? "", dateOfBirth, email, trustedEmail),
                registeredBy, DateTime.UtcNow, ct);
        }
        catch (PatientValidationException ex)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["errors"] = [.. ex.Errors] });
        }

        await scoreUseCase.ExecuteAsync(patient.Pid, ct);

        var view = await getUseCase.ExecuteAsync(patient.Uuid, ct);
        return Results.Created($"/fhir/Patient/{patient.Uuid}", FhirPatientMapper.ToFhirPatient(view!));
    }
}
