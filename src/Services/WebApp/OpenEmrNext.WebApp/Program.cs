using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using OpenEmrNext.AuthFacade.Client;
using OpenEmrNext.PatientManagement.Client;
using OpenEmrNext.WebApp;
using OpenEmrNext.WebApp.Resilience;

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

builder.Services.AddMudServices();

await builder.Build().RunAsync();
