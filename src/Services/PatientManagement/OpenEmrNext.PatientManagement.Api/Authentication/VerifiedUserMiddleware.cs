using OpenEmrNext.PatientManagement.Application.Ports;

namespace OpenEmrNext.PatientManagement.Api.Authentication;

/// <summary>The signed-in user behind a request, as the gateway verified it (ADR 0075): their OpenEMR uuid and the legacy <c>users.id</c> it resolves to.</summary>
public sealed record VerifiedUser(Guid Uuid, long UserId);

/// <summary>
/// Requires a verified user on every <c>/api/v1</c> and <c>/fhir</c> route (ADR 0075), the same rule as Clinical Care. The gateway checks the session with AuthFacade and forwards the user's OpenEMR uuid as
/// <c>X-Authenticated-User</c>; the shared-secret handler has already proved the request came from the gateway, which is why the header can be believed. A missing or malformed header is <b>401</b>; a uuid that matches
/// no user, or an account legacy has deactivated, is <b>403</b>. Middleware, not an endpoint filter, so nothing about the request (its body included) is judged before the caller is known.
/// </summary>
internal sealed class VerifiedUserMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Authenticated-User";
    public const string ItemKey = "OpenEmrNext.PatientManagement.VerifiedUser";

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

        http.Items[ItemKey] = new VerifiedUser(uuid, user.Id);
        await next(http);
    }

    private static Task Refuse(HttpContext http, string title, string detail, int status) =>
        Results.Problem(title: title, detail: detail, statusCode: status).ExecuteAsync(http);
}

internal static class VerifiedUserExtensions
{
    /// <summary>The user the gateway verified for this request, or <c>null</c> outside an authenticated route.</summary>
    public static VerifiedUser? VerifiedUser(this HttpContext http) => http.Items[VerifiedUserMiddleware.ItemKey] as VerifiedUser;

    /// <summary>
    /// The user id a request acts as: the verified user, whatever the body says (ADR 0075). The body's id is read only when there is no verified user, which no HTTP route allows, so a non-HTTP caller keeps working.
    /// </summary>
    public static long ActingUserId(this HttpContext http, long? requested) => http.VerifiedUser()?.UserId ?? requested ?? 0;

    /// <summary>Applies the middleware to the two route prefixes this service serves; the OpenAPI document is not a user's request.</summary>
    public static IApplicationBuilder UseVerifiedUser(this IApplicationBuilder app) =>
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/api/v1", StringComparison.OrdinalIgnoreCase) || context.Request.Path.StartsWithSegments("/fhir", StringComparison.OrdinalIgnoreCase),
            branch => branch.UseMiddleware<VerifiedUserMiddleware>());
}
