namespace OpenEmrNext.AuthFacade.Api.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/hello", () => Results.Ok(new { message = "Hello from AuthFacade" }))
            .AllowAnonymous();

        return app;
    }
}
