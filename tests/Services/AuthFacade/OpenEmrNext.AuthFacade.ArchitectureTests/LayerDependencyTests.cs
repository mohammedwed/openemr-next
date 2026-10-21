using System.Reflection;
using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Application.Abstractions;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using OpenEmrNext.AuthFacade.Infrastructure.Security;

namespace OpenEmrNext.AuthFacade.ArchitectureTests;

/// <summary>
/// Enforces the layer rules from docs/domain-driven-design.md: the domain depends on
/// nothing but the base class library, the application depends only on the domain,
/// infrastructure implements the application's ports, and only the API hosts everything.
/// </summary>
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

    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(name => name.Name!);

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
