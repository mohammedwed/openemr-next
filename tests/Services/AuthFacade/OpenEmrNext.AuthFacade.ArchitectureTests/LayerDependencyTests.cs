using System.Reflection;
using OpenEmrNext.AuthFacade.Api;
using OpenEmrNext.AuthFacade.Composition;
using OpenEmrNext.AuthFacade.Application.Authentication;
using OpenEmrNext.AuthFacade.Domain.ValueObjects;
using OpenEmrNext.AuthFacade.Infrastructure.Security;
using OpenEmrNext.AuthFacade.Application.Ports;

namespace OpenEmrNext.AuthFacade.ArchitectureTests;

/// <summary>
/// Checks the assembly reference graph against the layer rules in
/// docs/domain-driven-design.md. Each test reads the compiled assemblies' references, so a
/// forbidden reference fails the build even if the code never uses it.
/// </summary>
/// <remarks>
/// Rules enforced: the domain references only the base class library; the ports reference only
/// the domain and the base class library; the application references only the domain, the ports,
/// the base class library, and DI abstractions; neither references infrastructure or the API;
/// infrastructure does not reference the API or the application, and reaches the ports directly;
/// the domain, ports and application do not reference ASP.NET Core, EF Core, or IdentityModel;
/// the domain references no ports; infrastructure and composition do not reference ASP.NET Core;
/// composition does not reference the API; only hosts reference composition.
/// No test checks that infrastructure implements the application's interfaces.
/// </remarks>
public class LayerDependencyTests
{
    private const string DomainName = "OpenEmrNext.AuthFacade.Domain";
    private const string PortsName = "OpenEmrNext.AuthFacade.Application.Ports";
    private const string ApplicationName = "OpenEmrNext.AuthFacade.Application";
    private const string InfrastructureName = "OpenEmrNext.AuthFacade.Infrastructure";
    private const string ApiName = "OpenEmrNext.AuthFacade.Api";
    private const string CompositionName = "OpenEmrNext.AuthFacade.Composition";

    private static readonly Assembly Domain = typeof(Username).Assembly;
    private static readonly Assembly Ports = typeof(IIdentityProvider).Assembly;
    private static readonly Assembly Application = typeof(PasswordLoginHandler).Assembly;
    private static readonly Assembly Infrastructure = typeof(JwtOptions).Assembly;
    private static readonly Assembly Api = typeof(AuthFacadeApiExtensions).Assembly;
    private static readonly Assembly Composition = typeof(CompositionServiceCollectionExtensions).Assembly;

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
    public void Ports_reference_only_the_domain_and_base_class_library()
    {
        Assert.All(References(Ports), name => Assert.True(
            name == DomainName ||
            IsBaseClassLibrary(name) ||
            name == "System.Private.CoreLib",
            $"Ports must not reference '{name}'."));
    }

    [Fact]
    public void Application_references_only_domain_ports_base_class_library_and_di_abstractions()
    {
        Assert.All(References(Application), name => Assert.True(
            name == DomainName ||
            name == PortsName ||
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
    public void Infrastructure_does_not_reference_the_application()
    {
        Assert.DoesNotContain(ApplicationName, References(Infrastructure));
    }

    [Fact]
    public void Domain_ports_and_application_do_not_reference_the_web_stack()
    {
        var forbidden = References(Domain).Concat(References(Ports)).Concat(References(Application))
            .Where(name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
                           name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ||
                           name.StartsWith("Microsoft.IdentityModel", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(forbidden);
    }

    [Fact]
    public void Nothing_references_the_api_except_the_api_itself()
    {
        var referencing = new[] { Domain, Ports, Application, Infrastructure, Composition }
            .Where(assembly => References(assembly).Contains(ApiName))
            .Select(assembly => assembly.GetName().Name);

        Assert.Empty(referencing);
    }

    [Fact]
    public void Domain_does_not_reference_ports()
    {
        Assert.DoesNotContain(PortsName, References(Domain));
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
        var referencing = new[] { Domain, Ports, Application, Infrastructure }
            .Where(assembly => References(assembly).Contains(CompositionName))
            .Select(assembly => assembly.GetName().Name);

        Assert.Empty(referencing);
    }
}
