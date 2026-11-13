using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Infrastructure;
using OpenEmrNext.PatientManagement.Composition;

namespace OpenEmrNext.PatientManagement.Api;

/// <summary>Wires this host's own HTTP-layer concerns on top of <see cref="CompositionServiceCollectionExtensions.AddPatientManagement"/>.</summary>
public static class PatientManagementApiExtensions
{
    public static WebApplicationBuilder AddPatientManagementHttpDependencies(this WebApplicationBuilder builder)
    {
        builder.Services.AddPatientManagement(builder.Configuration);

        builder.Services.AddDbContext<PatientManagementDbContext>((provider, options) =>
        {
            var db = provider.GetRequiredService<IOptions<PatientManagementDbOptions>>().Value;
            var connectionString = "Server=" + db.Host +
                ";Port=" + db.Port +
                ";Database=" + db.Database +
                ";User=" + db.User +
                ";Password=" + db.Password + ";";
            // Matches the real mariadb:12.3.3 image this stack runs (.env.example's MARIADB_IMAGE),
            // same pin AuthFacade's own DbContext already uses.
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(12, 3, 3)));
        });

        // The only place the port (Application.Ports.IPatientRegistry) is wired to its real adapter
        // (Infrastructure's EfPatientRegistry) -- Application itself never sees this choice.
        builder.Services.AddScoped<IPatientRegistry, EfPatientRegistry>();
        builder.Services.AddScoped<RegisterPatientUseCase>();

        // Same pattern for the duplicate-detection port: Application only ever sees IDuplicateScorer.
        builder.Services.AddScoped<IDuplicateScorer, EfDuplicateScorer>();
        builder.Services.AddScoped<ScorePatientForDuplicatesUseCase>();

        // Same pattern again for insurance.
        builder.Services.AddScoped<IInsurancePolicyRepository, EfInsurancePolicyRepository>();
        builder.Services.AddScoped<AddInsurancePolicyUseCase>();
        builder.Services.AddScoped<GetEffectiveInsuranceUseCase>();
        builder.Services.AddScoped<SwapInsuranceUseCase>();

        // Advance directives: the file store is a plain adapter (no Composition dependency allowed
        // from Infrastructure), so its one config value is read here, at the composition root, and
        // handed in directly -- the same way the DbContext's connection string above is built from
        // options and handed to EF Core rather than EF reaching into Composition itself.
        builder.Services.AddScoped<IAdvanceDirectiveFileStore>(provider =>
            new LocalDiskAdvanceDirectiveFileStore(
                provider.GetRequiredService<IOptions<AdvanceDirectiveStorageOptions>>().Value.RepositoryRoot));
        builder.Services.AddScoped<IAdvanceDirectiveRepository, EfAdvanceDirectiveRepository>();
        builder.Services.AddScoped<UploadAdvanceDirectiveDocumentUseCase>();
        builder.Services.AddScoped<GetAdvanceDirectiveDocumentsUseCase>();
        builder.Services.AddScoped<DownloadAdvanceDirectiveDocumentUseCase>();
        builder.Services.AddScoped<SetAdvanceDirectiveStatusUseCase>();
        builder.Services.AddScoped<GetAdvanceDirectiveStatusUseCase>();

        // Same pattern again for the FHIR Patient API. Create/update go through IPatientRegistry
        // above -- this port is read-only.
        builder.Services.AddScoped<IPatientFhirRepository, EfPatientFhirRepository>();
        builder.Services.AddScoped<GetPatientFhirRecordUseCase>();
        builder.Services.AddScoped<SearchPatientFhirRecordsUseCase>();

        builder.Services.AddOpenApi();

        return builder;
    }
}
