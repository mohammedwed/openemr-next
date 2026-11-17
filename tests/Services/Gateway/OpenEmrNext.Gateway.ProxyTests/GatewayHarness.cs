using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OpenEmrNext.Gateway.ProxyTests;

/// <summary>One request a fake backend received: enough to prove what the gateway did and did not forward.</summary>
internal sealed record Seen(string Method, string Path, IReadOnlyDictionary<string, string> Headers);

/// <summary>A real Kestrel server standing in for one of the gateway's backends, recording every request it gets.</summary>
internal sealed class FakeBackend : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<Seen> _seen = new();

    public string Name { get; }
    public string Address { get; }
    public IReadOnlyList<Seen> Requests => [.. _seen];

    private FakeBackend(string name, WebApplication app, string address)
    {
        Name = name;
        _app = app;
        Address = address;
    }

    public static async Task<FakeBackend> StartAsync(string name, Func<HttpContext, Task<bool>>? handler = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        FakeBackend? self = null;
        app.Run(async context =>
        {
            self!._seen.Enqueue(new Seen(
                context.Request.Method, context.Request.Path.Value ?? "",
                context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase)));

            if (handler is not null && await handler(context))
            {
                return;
            }

            await context.Response.WriteAsJsonAsync(new { backend = name });
        });

        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        self = new FakeBackend(name, app, address);
        return self;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();
}

/// <summary>
/// The real gateway, in process, proxying to fake backends over real HTTP. AuthFacade's fake answers <c>/api/v1/me</c> from the cookie it is given, so a test chooses what the session check sees:
/// <c>session=ok-{uuid}</c> is a valid session for that user, <c>session=revoked</c> is refused, <c>session=boom</c> is a server error, <c>session=odd</c> is a subject that is not a uuid.
/// </summary>
internal sealed class GatewayHarness : IAsyncDisposable
{
    public const string PatientSecret = "patient-secret";
    public const string ClinicalSecret = "clinical-secret";

    private readonly Dictionary<string, string?> _previousEnvironment = [];
    private WebApplicationFactory<Program> _factory = null!;

    public FakeBackend AuthFacade { get; private set; } = null!;
    public FakeBackend PatientManagement { get; private set; } = null!;
    public FakeBackend ClinicalCare { get; private set; } = null!;
    public FakeBackend WebApp { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public static async Task<GatewayHarness> StartAsync(bool authFacadeReachable = true)
    {
        var harness = new GatewayHarness();
        harness.AuthFacade = await FakeBackend.StartAsync("authfacade", AuthFacadeAnswer);
        harness.PatientManagement = await FakeBackend.StartAsync("patientmanagement");
        harness.ClinicalCare = await FakeBackend.StartAsync("clinicalcare");
        harness.WebApp = await FakeBackend.StartAsync("webapp");

        // The gateway reads its settings at startup, before a test host could inject any, so they go in through the environment (restored on dispose).
        harness.SetEnvironment("AuthFacade__BaseUrl", authFacadeReachable ? harness.AuthFacade.Address : "http://127.0.0.1:1");
        harness.SetEnvironment("PatientManagement__BaseUrl", harness.PatientManagement.Address);
        harness.SetEnvironment("ClinicalCare__BaseUrl", harness.ClinicalCare.Address);
        harness.SetEnvironment("WebApp__BaseUrl", harness.WebApp.Address);
        harness.SetEnvironment("InternalGateway__Secret", PatientSecret);
        harness.SetEnvironment("ClinicalCare__Secret", ClinicalSecret);

        harness._factory = new WebApplicationFactory<Program>();
        // https, so the gateway's own redirect to https does not answer first.
        harness.Client = harness._factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        return harness;
    }

    private void SetEnvironment(string name, string value)
    {
        _previousEnvironment[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    private static async Task<bool> AuthFacadeAnswer(HttpContext context)
    {
        if (context.Request.Path != "/api/v1/me")
        {
            return false;   // any other AuthFacade route: the default answer
        }

        var cookie = context.Request.Headers.Cookie.ToString();
        var token = cookie.Split(';').Select(c => c.Trim()).FirstOrDefault(c => c.StartsWith("session=", StringComparison.Ordinal))?["session=".Length..];
        switch (token)
        {
            case null or "revoked":
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return true;
            case "boom":
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                return true;
            case "odd":
                await context.Response.WriteAsJsonAsync(new Dictionary<string, string> { [SessionVerifier.SubjectProperty] = "not-a-uuid", ["name"] = "Odd" });
                return true;
            default:
                // The same shape AuthFacade's real MeResponse has (see AuthFacadeContractTests), named by the gateway's own constant.
                await context.Response.WriteAsJsonAsync(new Dictionary<string, string>
                {
                    [SessionVerifier.SubjectProperty] = token.StartsWith("ok-", StringComparison.Ordinal) ? token["ok-".Length..] : token,
                    ["name"] = "Dr Who"
                });
                return true;
        }
    }

    public HttpRequestMessage Request(HttpMethod method, string path, string? cookie = null, IDictionary<string, string>? headers = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (cookie is not null)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        foreach (var (name, value) in headers ?? new Dictionary<string, string>())
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return request;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        foreach (var (name, value) in _previousEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        await AuthFacade.DisposeAsync();
        await PatientManagement.DisposeAsync();
        await ClinicalCare.DisposeAsync();
        await WebApp.DisposeAsync();
    }
}
