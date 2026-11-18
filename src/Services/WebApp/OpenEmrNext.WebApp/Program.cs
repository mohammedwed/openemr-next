using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using OpenEmrNext.AuthFacade.Client;
using OpenEmrNext.ClinicalCare.Client;
using OpenEmrNext.PatientManagement.Client;
using OpenEmrNext.WebApp;
using OpenEmrNext.WebApp.Resilience;
using OpenEmrNext.WebApp.Theme;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Deliberately the app's own base address, not AuthFacade's or PatientManagement's -- this app is
// served BY the gateway (same origin, ADR 0032/0055), which routes each service's own path prefixes
// server-side, so a relative API call and the AuthFacadeSession cookie it carries both resolve
// correctly without any CORS or cross-site cookie configuration. Each dependency gets its own
// HttpClient and its own TransientFailureCircuitBreakingHandler instance (independent breaker state,
// so one dependency failing never trips the other's breaker) sitting on the HttpClient itself, not
// around the generated client, so it survives either client being regenerated from a fresh OpenAPI
// snapshot.
builder.Services.AddScoped(sp => new HttpClient(new TransientFailureCircuitBreakingHandler { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});
builder.Services.AddScoped<IAuthFacadeClient>(sp => new AuthFacadeClient(sp.GetRequiredService<HttpClient>()));

builder.Services.AddScoped<IPatientManagementClient>(_ => new PatientManagementClient(
    new HttpClient(new TransientFailureCircuitBreakingHandler { InnerHandler = new HttpClientHandler() })
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    }));

// Same shape as PatientManagement above: the gateway routes /api/v1/encounters/** to ClinicalCare, and this
// client gets its own HttpClient and breaker so a ClinicalCare outage never trips the patient pages' breaker.
// A creating POST carries an Idempotency-Key while a dialog's save is in progress (ADR 0077), so a retried save is answered, not repeated.
builder.Services.AddScoped<OpenEmrNext.WebApp.Shared.IdempotencyAttempt>();
// The clock dialogs read their default times from, injected so a test can pin it. Times are UTC (OpenEmrNext.WebApp.Shared.UtcTime).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IClinicalCareClient>(sp => new ClinicalCareClient(
    new HttpClient(new OpenEmrNext.WebApp.Resilience.IdempotencyKeyHandler(sp.GetRequiredService<OpenEmrNext.WebApp.Shared.IdempotencyAttempt>())
    {
        InnerHandler = new TransientFailureCircuitBreakingHandler { InnerHandler = new HttpClientHandler() }
    })
    {
        BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
    }));

builder.Services.AddMudServices();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<OpenEmrNext.WebApp.Shared.PatientPageContext>();

await builder.Build().RunAsync();
