using OpenEmrNext.AuthFacade.Client;

namespace OpenEmrNext.WebApp.Shared;

/// <summary>
/// Pages that gate their own data on sign-in (the patient list and detail) share this check, so they
/// agree on what "signed in" means instead of each repeating the same try/catch.
/// </summary>
public static class AuthFacadeClientExtensions
{
    /// <summary>
    /// Returns the current user, or <c>null</c> when there is no valid session. Only an API response
    /// counts as "signed out": a network failure still throws, so the caller can show an outage instead.
    /// </summary>
    public static async Task<MeResponse?> TryGetCurrentUserAsync(this IAuthFacadeClient client)
    {
        try
        {
            return await client.GetCurrentUserAsync();
        }
        catch (ApiException)
        {
            return null;
        }
    }
}
