using System.Reflection;
using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Application.Ports;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

namespace OpenEmrNext.AuthFacade.ArchitectureTests;

/// <summary>
/// Checks the assembly reference graph against the layer rules in
/// docs/domain-driven-design.md. Each test reads the compiled assemblies' references, so a
/// forbidden reference fails the build even if the code never uses it.
/// </summary>
/// <remarks>
/// Rules enforced: the domain references only the base class library; the application
/// references only the domain, the base class library, and DI abstractions; neither references
/// infrastructure or the API; infrastructure does not reference the API; the domain and
/// application do not reference ASP.NET Core, EF Core, or IdentityModel. No test checks that
/// infrastructure implements the application's interfaces.
/// </remarks>
public class LayerDependencyTests
{
    private const string DomainName = "OpenEmrNext.AuthFacade.Domain";
    private const string ApplicationName = "OpenEmrNext.AuthFacade.Application";
    private const string InfrastructureName = "OpenEmrNext.AuthFacade.Infrastructure";
    private const string ApiName = "OpenEmrNext.AuthFacade.Api";

    private static readonly Assembly Domain = typeof(Username).Assembly;
    private static readonly Assembly Application = typeof(IOpenEmrIdentityProvider).Assembly;
    private static readonly Assembly Infrastructure = typeof(JwtOptions).Assembly;
    private static readonly Assembly Api = typeof(AuthFacadeApiExtensions).Assembly;

    /// <summary>Returns the simple names of the assemblies that <paramref name="assembly"/> references.</summary>
    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!);

    /// <summary>True for <c>netstandard</c> and assemblies whose names start with <c>System.</c>.</summary>
    private static bool IsBaseClassLibrary(string name) =>
        name == "netstandard" || name == "System" || name.StartsWith("System.", StringComparison.Ordinal);

    [Fact]
    public void Domain_references_only_the_base_class_library()
    {
        Assert.All(References(Domain), name => Assert.True(
            IsBaseClassLibrary(name) || name == "System.Private.CoreLib",
            $"Domain must not reference '{name}'."));
    }

    [Fact]
    public void Application_references_only_domain_base_class_library_and_di_abstractions()
    {
        Assert.All(References(Application), name => Assert.True(
            name == DomainName ||
            IsBaseClassLibrary(name) ||
            name == "Microsoft.Extensions.DependencyInjection.Abstractions",
            $"Application must not reference '{name}'."));
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_api()
    {
        var references = References(Application).ToList();

        Assert.DoesNotContain(InfrastructureName, references);
        Assert.DoesNotContain(ApiName, references);
    }

    [Fact]
    public void Infrastructure_does_not_reference_the_api()
    {
        Assert.DoesNotContain(ApiName, References(Infrastructure));
    }

    [Fact]
    public void Domain_and_application_do_not_reference_the_web_stack()
    {
        var forbidden = References(Domain).Concat(References(Application))
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                           name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                           name.StartsWith("Microsoft.IdentityModel", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Nothing_references_the_api_except_the_api_itself()
    {
        var referencing = new[] { Domain, Application, Infrastructure }
            .Where(assembly => References(assembly).Contains(ApiName))
            .Select(assembly => assembly.GetName().Name);

        Assert.Empty(referencing);
    }
}
