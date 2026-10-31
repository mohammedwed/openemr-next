using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using OpenEmrNext.AuthFacade.Client;
using OpenEmrNext.WebApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Deliberately the app's own base address, not AuthFacade's -- this app is served BY AuthFacade (same
// origin, ADR 0032), so a relative API call and the AuthFacadeSession cookie it carries both resolve
// correctly without any CORS or cross-site cookie configuration.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IAuthFacadeClient>(sp => new AuthFacadeClient(sp.GetRequiredService<HttpClient>()));
builder.Services.AddMudServices();

await builder.Build().RunAsync();
