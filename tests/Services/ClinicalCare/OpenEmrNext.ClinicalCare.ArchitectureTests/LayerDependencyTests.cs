using System.Reflection;

namespace OpenEmrNext.ClinicalCare.ArchitectureTests;

/// <summary>
/// Checks the compiled assembly reference graph against the ports-and-adapters layering (playbook ARC-10,
/// the same approach as PatientManagement's own <c>LayerDependencyTests</c>). A forbidden reference fails
/// the build even if the code never calls anything through it.
/// </summary>
/// <remarks>
/// Rules enforced: dependencies point inward only. <c>Domain</c> references nothing of ours.
/// <c>Domain.ReadModels</c> (ADR 0054) references only <c>Domain</c>: the write-side aggregates never reference the
/// read side back. <c>Application.Ports</c> references only <c>Domain</c> and <c>Domain.ReadModels</c>.
/// <c>Application</c> references those plus <c>Ports</c>, never <c>Infrastructure</c> or the API; it only ever sees a port. <c>Infrastructure</c>
/// implements the ports but never references <c>Application</c> or the API. <c>Composition</c> references
/// neither the API nor ASP.NET Core.
/// <para>
/// Assemblies are loaded by name, so no layer needs a type anchor. The compiler records a reference only for an
/// assembly the code actually uses, so a layer with no code would satisfy these rules trivially;
/// <see cref="Every_layer_assembly_loads"/> guards against the other failure, a layer that was renamed or dropped
/// so a rule silently checks nothing.
/// </para>
/// </remarks>
public class LayerDependencyTests
{
    private const string Prefix = "OpenEmrNext.ClinicalCare";
    private const string DomainName = Prefix + ".Domain";
    private const string ReadModelsName = Prefix + ".Domain.ReadModels";
    private const string PortsName = Prefix + ".Application.Ports";
    private const string ApplicationName = Prefix + ".Application";
    private const string InfrastructureName = Prefix + ".Infrastructure";
    private const string CompositionName = Prefix + ".Composition";
    private const string ApiName = Prefix + ".Api";

    private static Assembly Load(string name) => Assembly.Load(new AssemblyName(name));

    /// <summary>The simple names of this service's own assemblies that <paramref name="assembly"/> references.</summary>
    private static List<string> OwnReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal))
            .ToList();

    [Theory]
    [InlineData(DomainName)]
    [InlineData(ReadModelsName)]
    [InlineData(PortsName)]
    [InlineData(ApplicationName)]
    [InlineData(InfrastructureName)]
    [InlineData(CompositionName)]
    [InlineData(ApiName)]
    public void Every_layer_assembly_loads(string assemblyName)
    {
        var assembly = Load(assemblyName);

        Assert.Equal(assemblyName, assembly.GetName().Name);
    }

    [Fact]
    public void Domain_references_none_of_this_services_own_assemblies()
    {
        Assert.Empty(OwnReferences(Load(DomainName)));
    }

    [Fact]
    public void ReadModels_references_only_domain()
    {
        var forbidden = OwnReferences(Load(ReadModelsName)).Where(name => name != DomainName).ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Ports_references_only_domain_and_read_models()
    {
        var forbidden = OwnReferences(Load(PortsName))
            .Where(name => name != DomainName && name != ReadModelsName)
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_the_api()
    {
        var forbidden = OwnReferences(Load(ApplicationName))
            .Where(name => name == InfrastructureName || name == ApiName)
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Infrastructure_does_not_reference_application_or_the_api()
    {
        var forbidden = OwnReferences(Load(InfrastructureName))
            .Where(name => name == ApplicationName || name == ApiName)
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Composition_does_not_reference_the_api_or_aspnetcore()
    {
        var assembly = Load(CompositionName);
        var forbidden = assembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => name == ApiName || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }
}
