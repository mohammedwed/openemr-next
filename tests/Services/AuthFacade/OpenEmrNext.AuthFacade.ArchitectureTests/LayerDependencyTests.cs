using System.Reflection;
using OpenEmrNext.AuthFacade.Composition;

namespace OpenEmrNext.AuthFacade.ArchitectureTests;

/// <summary>
/// Checks the assembly reference graph against the layer rules in
/// docs/domain-driven-design.md. Each test reads the compiled assemblies' references, so a
/// forbidden reference fails the build even if the code never uses it.
/// </summary>
/// <remarks>
/// Rules enforced: composition (binds and validates the shared OpenEMR configuration) does not
/// reference the API or ASP.NET Core — it is the one place configuration is read, and the API
/// host is the only thing that may reference it, never the other way around.
/// </remarks>
public class LayerDependencyTests
{
    private const string ApiName = "OpenEmrNext.AuthFacade.Api";

    private static readonly Assembly Composition = typeof(CompositionServiceCollectionExtensions).Assembly;

    /// <summary>Returns the simple names of the assemblies that <paramref name="assembly"/> references.</summary>
    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!);

    [Fact]
    public void Composition_does_not_reference_the_api_or_aspnetcore()
    {
        var forbidden = References(Composition)
            .Where(name => name == ApiName || name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }
}
