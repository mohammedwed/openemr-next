using System.Reflection;
using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Composition;
using OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

namespace OpenEmrNext.AuthFacade.ArchitectureTests;

/// <summary>
/// Checks the assembly reference graph against the layer rules in
/// docs/domain-driven-design.md. Each test reads the compiled assemblies' references, so a
/// forbidden reference fails the build even if the code never uses it.
/// </summary>
/// <remarks>
/// Rules enforced: infrastructure (shared OpenEMR configuration) does not reference the API or
/// ASP.NET Core; composition does not reference the API or ASP.NET Core; only hosts reference
/// composition; nothing references the API except the API itself.
/// </remarks>
public class LayerDependencyTests
{
    private const string InfrastructureName = "OpenEmrNext.AuthFacade.Infrastructure";
    private const string ApiName = "OpenEmrNext.AuthFacade.Api";
    private const string CompositionName = "OpenEmrNext.AuthFacade.Composition";

    private static readonly Assembly Infrastructure = typeof(OpenEmrOptions).Assembly;
    private static readonly Assembly Api = typeof(AuthFacadeApiExtensions).Assembly;
    private static readonly Assembly Composition = typeof(CompositionServiceCollectionExtensions).Assembly;

    /// <summary>Returns the simple names of the assemblies that <paramref name="assembly"/> references.</summary>
    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!);

    [Fact]
    public void Infrastructure_does_not_reference_the_api()
    {
        Assert.DoesNotContain(ApiName, References(Infrastructure));
    }

    [Fact]
    public void Infrastructure_does_not_reference_aspnetcore()
    {
        var aspNet = References(Infrastructure)
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(aspNet);
    }

    [Fact]
    public void Composition_does_not_reference_the_api_or_aspnetcore()
    {
        var forbidden = References(Composition)
            .Where(name => name == ApiName || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Only_hosts_reference_composition()
    {
        var referencing = new[] { Infrastructure }
            .Where(assembly => References(assembly).Contains(CompositionName))
            .Select(assembly => assembly.GetName().Name);

        Assert.Empty(referencing);
    }

    [Fact]
    public void Nothing_references_the_api_except_the_api_itself()
    {
        var referencing = new[] { Infrastructure, Composition }
            .Where(assembly => References(assembly).Contains(ApiName))
            .Select(assembly => assembly.GetName().Name);

        Assert.Empty(referencing);
    }
}
