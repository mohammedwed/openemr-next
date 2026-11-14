using System.Reflection;
using OpenEmrNext.PatientManagement.Application;
using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Composition;
using OpenEmrNext.PatientManagement.Domain;
using OpenEmrNext.PatientManagement.Domain.Fhir.ReadModels;
using OpenEmrNext.PatientManagement.Infrastructure;

namespace OpenEmrNext.PatientManagement.ArchitectureTests;

/// <summary>
/// Checks the assembly reference graph against the ports-and-adapters layering this service uses --
/// the same rules, and the same reflection-over-compiled-assemblies approach, as AuthFacade's own
/// (now superseded) <c>Application.Ports</c> layering (ADR 0007, ADR 0010). Each test reads the
/// compiled assemblies' references, so a forbidden reference fails the build even if the code never
/// actually calls anything from it.
/// </summary>
/// <remarks>
/// Rules enforced: dependencies point inward only. <c>Domain</c> references nothing of ours.
/// <c>Domain.ReadModels</c> (ADR 0054) references only <c>Domain</c> -- the write-side aggregates
/// never reference the read side back. <c>Application.Ports</c> references only <c>Domain</c> and
/// <c>Domain.ReadModels</c>. <c>Application</c> references those plus <c>Ports</c>, never
/// <c>Infrastructure</c> directly -- it only ever sees the port. <c>Infrastructure</c> implements the
/// ports but never references <c>Application</c>. <c>Composition</c> references neither the API nor
/// ASP.NET Core.
/// </remarks>
public class LayerDependencyTests
{
    private const string DomainName = "OpenEmrNext.PatientManagement.Domain";
    private const string ReadModelsName = "OpenEmrNext.PatientManagement.Domain.ReadModels";
    private const string PortsName = "OpenEmrNext.PatientManagement.Application.Ports";
    private const string ApplicationName = "OpenEmrNext.PatientManagement.Application";
    private const string InfrastructureName = "OpenEmrNext.PatientManagement.Infrastructure";
    private const string ApiName = "OpenEmrNext.PatientManagement.Api";

    private static readonly Assembly Domain = typeof(Patient).Assembly;
    private static readonly Assembly ReadModels = typeof(PatientFhirView).Assembly;
    private static readonly Assembly Ports = typeof(IPatientRegistry).Assembly;
    private static readonly Assembly Application = typeof(RegisterPatientUseCase).Assembly;
    private static readonly Assembly Infrastructure = typeof(EfPatientRegistry).Assembly;
    private static readonly Assembly Composition = typeof(CompositionServiceCollectionExtensions).Assembly;

    /// <summary>Returns the simple names of the assemblies that <paramref name="assembly"/> references.</summary>
    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!);

    [Fact]
    public void Domain_references_none_of_this_services_own_assemblies()
    {
        var forbidden = References(Domain)
            .Where(name => name.StartsWith("OpenEmrNext.PatientManagement", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void ReadModels_references_only_domain()
    {
        var forbidden = References(ReadModels)
            .Where(name => name.StartsWith("OpenEmrNext.PatientManagement", StringComparison.Ordinal) && name != DomainName)
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Ports_references_only_domain_and_read_models()
    {
        var forbidden = References(Ports)
            .Where(name => name.StartsWith("OpenEmrNext.PatientManagement", StringComparison.Ordinal)
                && name != DomainName && name != ReadModelsName)
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_the_api()
    {
        var forbidden = References(Application)
            .Where(name => name == InfrastructureName || name == ApiName)
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Infrastructure_does_not_reference_application_or_the_api()
    {
        var forbidden = References(Infrastructure)
            .Where(name => name == ApplicationName || name == ApiName)
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Composition_does_not_reference_the_api_or_aspnetcore()
    {
        var forbidden = References(Composition)
            .Where(name => name == ApiName || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }
}
