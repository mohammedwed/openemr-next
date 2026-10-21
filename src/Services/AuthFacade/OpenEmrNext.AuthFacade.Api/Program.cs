using OpenEmrNext.AuthFacade.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddAuthFacadeApi();

var app = builder.Build();
app.UseAuthFacadeApi();

app.Run();
