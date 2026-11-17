using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenEmrNext.ClinicalCare.Api.Authentication;
using OpenEmrNext.ClinicalCare.Api.OpenApi;
using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Composition;
using OpenEmrNext.ClinicalCare.Infrastructure;

namespace OpenEmrNext.ClinicalCare.Api;

/// <summary>Wires this host's own HTTP-layer concerns on top of <see cref="CompositionServiceCollectionExtensions.AddClinicalCare"/>.</summary>
public static class ClinicalCareApiExtensions
{
    public static WebApplicationBuilder AddClinicalCareHttpDependencies(this WebApplicationBuilder builder)
    {
        builder.Services.AddClinicalCare(builder.Configuration);

        builder.Services.AddDbContext<ClinicalCareDbContext>((provider, options) =>
        {
            var db = provider.GetRequiredService<IOptions<ClinicalCareDbOptions>>().Value;
            var connectionString = "Server=" + db.Host +
                ";Port=" + db.Port +
                ";Database=" + db.Database +
                ";User=" + db.User +
                ";Password=" + db.Password + ";";
            // Matches the real mariadb:12.3.3 image the stack runs (.env.example's MARIADB_IMAGE).
            //
            // EnableRetryOnFailure means every explicit transaction in the Infrastructure layer must run inside
            // Database.CreateExecutionStrategy().ExecuteAsync(...); EfEncounterRegistry and
            // EfEncounterIdentityAllocator both do (ADR 0056, OPS-9).
            options.UseMySql(connectionString, new MariaDbServerVersion(new Version(12, 3, 3)),
                mySqlOptions => mySqlOptions.EnableRetryOnFailure());
        });

        // The clock is an input (DOM-8): the use case asks this, never DateTime.Now.
        builder.Services.AddSingleton(TimeProvider.System);

        // The only place each port is wired to its real adapter; Application only ever sees the port.
        builder.Services.AddScoped<IPatientDirectory, EfPatientDirectory>();
        builder.Services.AddScoped<IPatientReferenceStore, EfPatientReferenceStore>();
        builder.Services.AddScoped<ConsumePatientRegisteredUseCase>();
        builder.Services.AddScoped<IEncounterCodeLists, EfEncounterCodeLists>();
        builder.Services.AddScoped<IFacilityDirectory, EfFacilityDirectory>();
        builder.Services.AddScoped<IEncounterIdentityAllocator, EfEncounterIdentityAllocator>();
        builder.Services.AddScoped<IAuditLog, EfAuditLog>();
        builder.Services.AddScoped<IEncounterRegistry, EfEncounterRegistry>();
        builder.Services.AddScoped<CreateEncounterUseCase>();
        builder.Services.AddScoped<IEncounterUpdater, EfEncounterUpdater>();
        builder.Services.AddScoped<UpdateEncounterUseCase>();
        builder.Services.AddScoped<IEncounterReader, EfEncounterReader>();
        builder.Services.AddScoped<GetEncounterUseCase>();
        builder.Services.AddScoped<ListPatientEncountersUseCase>();

        // Retention of this service's own bookkeeping (finding R-10).
        builder.Services.AddScoped<IRetentionStore, EfRetentionStore>();
        builder.Services.AddScoped(provider =>
        {
            var retention = provider.GetRequiredService<IOptions<RetentionOptions>>().Value;
            return new PurgeExpiredRecordsUseCase(
                provider.GetRequiredService<IRetentionStore>(),
                new RetentionCutoffs(retention.ProcessedEventDays, retention.PublishedOutboxDays, retention.UnpublishedOutboxDays),
                retention.BatchSize);
        });
        builder.Services.AddHostedService<RetentionHostedService>();

        // Vitals (slice 2).
        builder.Services.AddSingleton<IVitalsIdentityAllocator, GuidVitalsIdentityAllocator>();
        builder.Services.AddScoped<IVitalsRegistry, EfVitalsRegistry>();
        builder.Services.AddScoped<IVitalsReader, EfVitalsReader>();
        builder.Services.AddScoped<RecordVitalsUseCase>();
        builder.Services.AddScoped<GetVitalsUseCase>();
        builder.Services.AddScoped<ListEncounterVitalsUseCase>();

        // Anything an endpoint does not catch itself becomes a ProblemDetails 500, not ASP.NET's bare default
        // (OPS-8, API-4); Program.cs adds app.UseExceptionHandler(). Same minimal approach as ADR 0057.
        builder.Services.AddProblemDetails();

        // Shared-secret gate (ADR 0055 pattern); the handler is the only authentication scheme.
        builder.Services.AddAuthentication(InternalGatewayAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, InternalGatewayAuthenticationHandler>(
                InternalGatewayAuthenticationHandler.SchemeName, null);
        // A second, separate scheme for event delivery (ADR 0066, API-16). The default scheme above stays the gateway's, so
        // every public route still requires the gateway secret and rejects the producers'.
        builder.Services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, InternalEventsAuthenticationHandler>(
                InternalEventsAuthenticationHandler.SchemeName, null);
        builder.Services.AddAuthorization();

        // Shape validation on request DTOs; the Domain factories stay the source of truth (ARC-11).
        builder.Services.AddValidation();

        builder.Services.AddOpenApi(options => options.ConfigureClinicalCare());

        return builder;
    }
}
