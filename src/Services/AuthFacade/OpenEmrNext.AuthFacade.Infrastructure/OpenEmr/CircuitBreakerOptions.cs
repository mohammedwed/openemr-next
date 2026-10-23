using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.AuthFacade.Infrastructure.OpenEmr;

/// <summary>
/// Settings for the circuit breakers that guard calls to OpenEMR, bound from <c>OpenEmr:CircuitBreaker</c>.
/// Each breaker opens when the failure ratio in the sampling window reaches <see cref="FailureRatio"/>
/// once at least <see cref="MinimumThroughput"/> calls have been made in that window.
/// </summary>
public sealed class CircuitBreakerOptions : IValidatableObject
{
    /// <summary>The shortest sampling or break duration the resilience library accepts.</summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMilliseconds(500);

    /// <summary>The share of failed calls in the sampling window that opens the circuit. Must be above 0 and at most 1.</summary>
    public double FailureRatio { get; set; } = 0.5;

    /// <summary>The window over which failures are sampled. At least 500 milliseconds.</summary>
    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>The fewest calls in the window before the failure ratio applies. Must be at least 1.</summary>
    public int MinimumThroughput { get; set; } = 5;

    /// <summary>How long the circuit stays open before a trial call is let through. At least 500 milliseconds.</summary>
    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FailureRatio <= 0 || FailureRatio > 1)
        {
            yield return new ValidationResult(
                "OpenEmr:CircuitBreaker:FailureRatio must be above 0 and at most 1.",
                [nameof(FailureRatio)]);
        }

        if (MinimumThroughput < 1)
        {
            yield return new ValidationResult(
                "OpenEmr:CircuitBreaker:MinimumThroughput must be at least 1.",
                [nameof(MinimumThroughput)]);
        }

        if (SamplingDuration < MinimumDuration)
        {
            yield return new ValidationResult(
                "OpenEmr:CircuitBreaker:SamplingDuration must be at least 500 milliseconds.",
                [nameof(SamplingDuration)]);
        }

        if (BreakDuration < MinimumDuration)
        {
            yield return new ValidationResult(
                "OpenEmr:CircuitBreaker:BreakDuration must be at least 500 milliseconds.",
                [nameof(BreakDuration)]);
        }
    }
}
