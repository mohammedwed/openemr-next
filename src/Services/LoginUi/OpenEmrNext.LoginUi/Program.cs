using OpenEmrNext.LoginUi;
using OpenEmrNext.LoginUi.Options;

// A static login page: one button that links to AuthFacade's own /oidc/login endpoint (ADR 0026), which
// starts the browser authorization-code flow against OpenEMR. It holds no credentials and calls AuthFacade
// for nothing itself, so the page needs no interactive render mode.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

builder.Services.AddOptions<AuthFacadeOptions>()
    .Bind(builder.Configuration.GetSection(AuthFacadeOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>();

app.Run();
