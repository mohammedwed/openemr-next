using OpenEmrNext.ClinicalCare.Application;
using OpenEmrNext.ClinicalCare.Application.Ports;

namespace OpenEmrNext.ClinicalCare.Api.Authentication;

/// <summary>
/// Requires a verified user on every <c>/api/v1</c> route (ADR 0075). The gateway checks the session with AuthFacade and forwards the user's OpenEMR uuid as <c>X-Authenticated-User</c>; the shared-secret handler has
/// already proved the request came from the gateway, which is why the header can be believed. This resolves the uuid to the legacy user and makes them the acting provider and audit actor for the request. A missing or
/// malformed header is <b>401</b>; a uuid that matches no user, or an account legacy has deactivated, is <b>403</b>, never a silent anonymous write.
/// </summary>
/// <remarks>
/// Middleware, not an endpoint filter, on purpose: a filter runs after the request body has been bound and validated, so a caller with no identity and a bad body would be told about the body (400) before being
/// told to sign in. Here nothing about the request is judged until the caller is known. It runs after authorization, so a request without the gateway secret never reaches it.
/// </remarks>
internal sealed class VerifiedUserMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Authenticated-User";
    public const string ItemKey = "OpenEmrNext.ClinicalCare.VerifiedActor";

    public async Task InvokeAsync(HttpContext http, IUserDirectory users)
    {
        if (!Guid.TryParse(http.Request.Headers[HeaderName].FirstOrDefault(), out var uuid) || uuid == Guid.Empty)
        {
            await Refuse(http, "Sign-in is required.", "This request carries no verified user. Sign in with OpenEMR and try again.", StatusCodes.Status401Unauthorized);
            return;
        }

        var user = await users.FindByUuidAsync(uuid, http.RequestAborted);
        if (user is null)
        {
            await Refuse(http, "Not a known user.", "The signed-in account is not an OpenEMR user this service can act for.", StatusCodes.Status403Forbidden);
            return;
        }

        if (!user.Active)
        {
            await Refuse(http, "Account is inactive.", "This OpenEMR account is no longer active.", StatusCodes.Status403Forbidden);
            return;
        }

        http.Items[ItemKey] = new VerifiedActor(uuid, user.Id, user.Username);
        await next(http);
    }

    private static Task Refuse(HttpContext http, string title, string detail, int status) =>
        Results.Problem(title: title, detail: detail, statusCode: status).ExecuteAsync(http);
}

internal static class VerifiedUserExtensions
{
    /// <summary>Applies <see cref="VerifiedUserMiddleware"/> to <c>/api/v1</c> and <c>/fhir</c> (ADR 0079): the event delivery endpoint is service-to-service and has its own secret (ADR 0066), and the OpenAPI document is not a user's request.</summary>
    public static IApplicationBuilder UseVerifiedUser(this IApplicationBuilder app) =>
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase) || context.Request.Path.StartsWithSegments("/fhir", StringComparison.OrdinalIgnoreCase),
            branch => branch.UseMiddleware<VerifiedUserMiddleware>());
}
