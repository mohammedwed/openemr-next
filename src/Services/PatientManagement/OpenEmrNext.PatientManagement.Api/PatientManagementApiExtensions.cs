using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenEmrNext.PatientManagement.Api.Persistence;
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

        builder.Services.AddOpenApi();

        return builder;
    }
}
