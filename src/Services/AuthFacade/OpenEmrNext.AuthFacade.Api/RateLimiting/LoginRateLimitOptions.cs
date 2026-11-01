using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.AuthFacade.Api.RateLimiting;

/// <summary>
/// Limits how many requests one client address may send to <c>GET /oidc/login</c> and <c>/callback</c> in a
/// window, bound from <c>LoginRateLimit</c>. Every request counts, successful or not, so the browser
/// authorization-code flow (ADR 0026) cannot be driven faster than the limit allows.
/// </summary>
public sealed class LoginRateLimitOptions : IValidatableObject
{
    /// <summary>The configuration section these options are bound from.</summary>
    public const string SectionName = "LoginRateLimit";

    /// <summary>The most requests one client address may send in a window. Must be at least 1.</summary>
    public int PermitLimit { get; set; } = 10;

    /// <summary>The length of one fixed window. At least one second.</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PermitLimit < 1)
        {
            yield return new ValidationResult(
                "LoginRateLimit:PermitLimit must be at least 1.",
                [nameof(PermitLimit)]);
        }

        if (Window < TimeSpan.FromSeconds(1))
        {
            yield return new ValidationResult(
                "LoginRateLimit:Window must be at least one second.",
                [nameof(Window)]);
        }
    }
}
