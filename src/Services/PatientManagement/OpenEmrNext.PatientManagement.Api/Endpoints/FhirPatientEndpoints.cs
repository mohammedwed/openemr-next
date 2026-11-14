using Hl7.Fhir.Model;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Fhir;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
using OpenEmrNext.PatientManagement.Domain.Fhir.Rules;
using FhirPatient = Hl7.Fhir.Model.Patient;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>
/// FHIR R4 Patient endpoints, under <c>/fhir</c> rather than <c>/api/v1</c> -- FHIR resources are not
/// versioned the way this service's own internal API is, matching legacy's own
/// <c>/fhir/Patient</c> route convention. Backed by Firely's generated <see cref="Patient"/> model
/// (the official .NET FHIR SDK), not a hand-rolled shape -- every request/response here is real,
/// spec-conformant FHIR R4 JSON (<c>application/fhir+json</c>), serialized through
/// <see cref="FhirJson"/>. Pinned to the 5.11.x SDK generation specifically so
/// <see cref="FhirProfileValidator"/>'s validator works (see <c>docs/m4-domain-notes.md</c>'s FHIR SDK
/// section for why the newer 6.x line couldn't be used for this).
/// </summary>
/// <remarks>
/// Covers <c>GET</c> (read + search), <c>POST</c> (create, reusing
/// <see cref="RegisterPatientUseCase"/>, the same path plain registration already uses), and the
/// standard FHIR <c>$validate</c> operation against the real US Core Patient profile.
/// <c>PUT</c> (update) is deliberately not built in this slice: it would need a brand-new
/// <c>Patient.Update()</c>/persistence capability that doesn't exist anywhere in this service yet
/// (only <c>RegisterPatientUseCase</c>/<c>IPatientRegistry.RegisterAsync</c> do, both create-only) --
/// not a case of "reusing what exists," despite the parity plan's original assumption that it would
/// be. <c>$export</c>, <c>DELETE</c>, <c>$everything</c>, and <c>$match</c> are out of scope: the first
/// per the plan, the rest because legacy doesn't implement them either.
/// </remarks>
public static class FhirPatientEndpoints
{
    private const string FhirJsonContentType = "application/fhir+json";

    public static IEndpointRouteBuilder MapFhirPatientEndpoints(this IEndpointRouteBuilder app)
    {
        var fhir = app.MapGroup("/fhir");

        fhir.MapGet("/Patient/{uuid:guid}", GetPatient)
            .WithName("FhirGetPatient")
            .WithTags("Fhir")
            .WithSummary("Read a patient as a FHIR R4 Patient resource")
            .Produces<FhirPatient>(contentType: FhirJsonContentType)
            .Produces(StatusCodes.Status404NotFound);

        fhir.MapGet("/Patient", SearchPatients)
            .WithName("FhirSearchPatients")
            .WithTags("Fhir")
            .WithSummary("Search patients, returning a FHIR searchset Bundle")
            .Produces<Bundle>(contentType: FhirJsonContentType);

        fhir.MapPost("/Patient", CreatePatient)
            .WithName("FhirCreatePatient")
            .WithTags("Fhir")
            .WithSummary("Create a patient from a FHIR R4 Patient resource")
            .Produces<FhirPatient>(StatusCodes.Status201Created, FhirJsonContentType)
            .Produces<OperationOutcome>(StatusCodes.Status400BadRequest, FhirJsonContentType);

        fhir.MapPost("/Patient/$validate", ValidatePatient)
            .WithName("FhirValidatePatient")
            .WithTags("Fhir")
            .WithSummary("Validate a FHIR R4 Patient resource against the real US Core Patient profile")
            .Produces<OperationOutcome>(contentType: FhirJsonContentType)
            .Produces<OperationOutcome>(StatusCodes.Status400BadRequest, FhirJsonContentType);

        return app;
    }

    private static IResult FhirContent(Resource resource, int statusCode = StatusCodes.Status200OK) =>
        Results.Content(FhirJson.Serialize(resource), FhirJsonContentType, statusCode: statusCode);

    private static async Task<IResult> GetPatient(Guid uuid, GetPatientFhirRecordUseCase useCase, CancellationToken ct)
    {
        var view = await useCase.ExecuteAsync(uuid, ct);
        return view is null ? Results.NotFound() : FhirContent(FhirPatientMapper.ToFhirPatient(view));
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
        var patients = views.Select(FhirPatientMapper.ToFhirPatient).ToList();

        var bundle = new Bundle { Type = Bundle.BundleType.Searchset, Total = patients.Count };
        bundle.Entry.AddRange(patients.Select(p => new Bundle.EntryComponent
        {
            FullUrl = $"Patient/{p.Id}",
            Resource = p,
            Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match }
        }));

        return FhirContent(bundle);
    }

    private static async Task<IResult> CreatePatient(
        HttpRequest request, long registeredBy,
        RegisterPatientUseCase registerUseCase, ScorePatientForDuplicatesUseCase scoreUseCase,
        GetPatientFhirRecordUseCase getUseCase, CancellationToken ct)
    {
        FhirPatient incoming;
        try
        {
            incoming = await ParseBodyAsync<FhirPatient>(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FhirContent(ProblemOutcome($"Request body is not a valid FHIR Patient resource: {ex.Message}"), StatusCodes.Status400BadRequest);
        }

        // Ports parseFhirResource()'s subset: the official name (first entry if none is marked
        // "official," matching legacy's own fallback), telecom's email/email_direct (use=mobile ->
        // TrustedEmail, matching legacy's exact use-code mapping; everything else -> Email), gender,
        // and birthDate. PatientRegistration has no middle-name/title/phone fields at all, so a FHIR
        // resource carrying them silently drops them here -- an existing limitation of
        // PatientRegistration itself, not something this endpoint adds.
        var name = incoming.Name.FirstOrDefault(n => n.Use == HumanName.NameUse.Official) ?? incoming.Name.FirstOrDefault();
        var firstName = name?.Given.FirstOrDefault() ?? "";
        var lastName = name?.Family ?? "";

        string? email = null;
        string? trustedEmail = null;
        foreach (var contact in incoming.Telecom)
        {
            if (contact.System != ContactPoint.ContactPointSystem.Email) continue;
            if (contact.Use == ContactPoint.ContactPointUse.Mobile) trustedEmail = contact.Value;
            else email = contact.Value;
        }

        var gender = incoming.Gender?.ToString()?.ToLowerInvariant() ?? "";
        var dateOfBirth = DateOnly.TryParseExact(incoming.BirthDate, "yyyy-MM-dd", out var dob) ? dob : default;

        OpenEmrNext.PatientManagement.Domain.Patient registered;
        try
        {
            registered = await registerUseCase.ExecuteAsync(
                PatientRegistration.Create(firstName, lastName, gender, dateOfBirth, email, trustedEmail),
                registeredBy, DateTime.UtcNow, ct);
        }
        catch (PatientValidationException ex)
        {
            return FhirContent(ProblemOutcome(string.Join("; ", ex.Errors)), StatusCodes.Status400BadRequest);
        }

        await scoreUseCase.ExecuteAsync(registered.Pid, ct);

        var view = await getUseCase.ExecuteAsync(registered.Uuid, ct);
        return FhirContent(FhirPatientMapper.ToFhirPatient(view!), StatusCodes.Status201Created);
    }

    private static async Task<IResult> ValidatePatient(HttpRequest request, FhirProfileValidator validator, CancellationToken ct)
    {
        FhirPatient incoming;
        try
        {
            incoming = await ParseBodyAsync<FhirPatient>(request, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return FhirContent(ProblemOutcome($"Request body is not a valid FHIR Patient resource: {ex.Message}"), StatusCodes.Status400BadRequest);
        }

        var outcome = validator.Validate(incoming, FhirProfileValidator.UsCorePatientProfile);
        var isValid = outcome.Issue.All(i => i.Severity is not (OperationOutcome.IssueSeverity.Error or OperationOutcome.IssueSeverity.Fatal));
        return FhirContent(outcome, isValid ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
    }

    private static async Task<T> ParseBodyAsync<T>(HttpRequest request, CancellationToken ct) where T : Resource
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            throw new FormatException("Request body was empty.");
        return FhirJson.Parse<T>(body);
    }

    private static OperationOutcome ProblemOutcome(string diagnostics)
    {
        var outcome = new OperationOutcome();
        outcome.Issue.Add(new OperationOutcome.IssueComponent
        {
            Severity = OperationOutcome.IssueSeverity.Error,
            Code = OperationOutcome.IssueType.Invalid,
            Diagnostics = diagnostics
        });
        return outcome;
    }
}
