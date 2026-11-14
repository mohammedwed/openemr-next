using Microsoft.EntityFrameworkCore;
using OpenEmrNext.PatientManagement.Api.Endpoints;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Fhir;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
using OpenEmrNext.PatientManagement.Domain.Fhir.Rules;
using OpenEmrNext.PatientManagement.Infrastructure;
using Xunit;

namespace OpenEmrNext.PatientManagement.IntegrationTests;

/// <summary>
/// Proves the FHIR Patient read/search mapping against a real MariaDB, including the
/// <c>list_options</c>-resolved extensions (race, ethnicity, administrative sex, tribal affiliation,
/// language) -- not just that the SQL compiles.
/// </summary>
[Collection("PatientDataContainer")]
public class PatientFhirTests(PatientDataContainerFixture fixture)
{
    private static async Task<Patient> RegisterAsync(PatientManagementDbContext context, string? sex = "Male")
    {
        var registration = PatientRegistration.Create(
            "Test", $"Patient{Guid.NewGuid():N}", sex ?? "Male", new DateOnly(2000, 1, 1),
            $"{Guid.NewGuid():N}@example.com", null);
        return await new RegisterPatientUseCase(new EfPatientRegistry(context))
            .ExecuteAsync(registration, registeredBy: 1, DateTime.UtcNow, CancellationToken.None);
    }

    [Fact]
    public async Task A_freshly_registered_patient_resolves_to_legacy_default_unknowns()
    {
        var context = fixture.CreateDbContext();
        var patient = await RegisterAsync(context);
        var repository = new EfPatientFhirRepository(context);

        var view = await repository.GetByUuidAsync(patient.Uuid, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal("male", view!.Gender);
        Assert.Equal("M", view.BirthSexCode);
        Assert.Equal("UNK", view.Race.Code);
        Assert.Equal("", view.EthnicityText);
        Assert.Null(view.GenderIdentity);
        Assert.Equal("unknown", view.SexIdentified.Code);
        Assert.Equal("unknown", view.InterpreterNeeded.Code);
        Assert.Null(view.Language);
        Assert.Null(view.DeceasedDateTime);
        Assert.Null(view.GeneralPractitionerUuid);
    }

    [Fact]
    public async Task List_option_backed_fields_resolve_to_the_real_seeded_codes()
    {
        var context = fixture.CreateDbContext();
        var patient = await RegisterAsync(context);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE patient_data SET
                race = 'white', ethnicity = 'hispanic', sex_identified = 'Male',
                tribal_affiliations = 'navajo_nation', interpreter_needed = 'no', language = 'english',
                ss = '123-45-6789', title = 'Mr', suffix = 'Jr',
                street = '123 Main St', city = 'Anytown', state = 'CA', postal_code = '90210'
            WHERE pid = {patient.Pid}
            """);

        var repository = new EfPatientFhirRepository(context);
        var view = await repository.GetByUuidAsync(patient.Uuid, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal("2106-3", view!.Race.Code);
        Assert.Equal("White", view.Race.Display);
        Assert.Equal("2135-2", view.EthnicityOmb?.Code);
        Assert.Equal("Hispanic or Latino", view.EthnicityText);
        Assert.Equal("248152002", view.SexIdentified.Code);
        Assert.Equal("http://snomed.info/sct", view.SexIdentified.System);
        Assert.Equal("Navajo Nation (AZ/NM/UT)", view.TribalAffiliation?.Display);
        Assert.Equal("373067005", view.InterpreterNeeded.Code);
        Assert.NotNull(view.Language);
        Assert.Equal("en", view.Language!.Code);
        Assert.Equal("123-45-6789", view.Ssn);

        var fhirPatient = FhirPatientMapper.ToFhirPatient(view);
        Assert.Equal(patient.Uuid.ToString(), fhirPatient.Id);
        Assert.Contains(fhirPatient.Extension, e => e.Url.EndsWith("us-core-ethnicity"));
        Assert.Single(fhirPatient.Address);
        Assert.Equal("Anytown", fhirPatient.Address[0].City);
        // SSN + pubpid (always set at registration, to Pid's own value) -- two identifiers, not one.
        Assert.Equal(2, fhirPatient.Identifier.Count);
        Assert.Contains(fhirPatient.Identifier, i => i.Value == "123-45-6789");
    }

    [Fact]
    public async Task An_ethnicity_decline_omits_the_ombCategory_coding_but_keeps_the_text()
    {
        var context = fixture.CreateDbContext();
        var patient = await RegisterAsync(context);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE patient_data SET ethnicity = 'decline_to_specify' WHERE pid = {patient.Pid}");

        var repository = new EfPatientFhirRepository(context);
        var view = await repository.GetByUuidAsync(patient.Uuid, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Null(view!.EthnicityOmb);
        Assert.Equal("Declined To Specify", view.EthnicityText);
    }

    [Fact]
    public async Task A_deceased_patient_resolves_deceasedDateTime_not_deceasedBoolean()
    {
        var context = fixture.CreateDbContext();
        var patient = await RegisterAsync(context);
        var deceasedAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE patient_data SET deceased_date = {deceasedAt} WHERE pid = {patient.Pid}");

        var repository = new EfPatientFhirRepository(context);
        var view = await repository.GetByUuidAsync(patient.Uuid, CancellationToken.None);
        var fhirPatient = FhirPatientMapper.ToFhirPatient(view!);

        Assert.IsType<Hl7.Fhir.Model.FhirDateTime>(fhirPatient.Deceased);
    }

    [Fact]
    public async Task Searching_by_family_name_finds_the_registered_patient()
    {
        var context = fixture.CreateDbContext();
        var uniqueLastName = $"Searchable{Guid.NewGuid():N}";
        var registration = PatientRegistration.Create("Findme", uniqueLastName, "Female", new DateOnly(1990, 5, 5), null, null);
        var patient = await new RegisterPatientUseCase(new EfPatientRegistry(context))
            .ExecuteAsync(registration, registeredBy: 1, DateTime.UtcNow, CancellationToken.None);

        var repository = new EfPatientFhirRepository(context);
        var results = await repository.SearchAsync(new PatientFhirSearchCriteria(Family: uniqueLastName), CancellationToken.None);

        var found = Assert.Single(results);
        Assert.Equal(patient.Uuid, found.Uuid);
    }

    [Fact]
    public async Task Getting_an_unknown_uuid_returns_null()
    {
        var context = fixture.CreateDbContext();
        var repository = new EfPatientFhirRepository(context);

        var view = await repository.GetByUuidAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(view);
    }
}
