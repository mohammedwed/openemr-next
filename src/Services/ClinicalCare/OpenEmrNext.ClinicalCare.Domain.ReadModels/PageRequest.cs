using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Domain.ReadModels;

/// <summary>
/// Which slice of a list a caller wants. Every list is bounded (API-6, DOM-11): an unbounded read of a busy
/// patient's whole history is a request nobody can afford to serve.
/// </summary>
public sealed record PageRequest
{
    /// <summary>Used when the caller names no limit.</summary>
    public const int DefaultLimit = 25;

    /// <summary>(new) The most a caller may ask for in one page; legacy lists are unbounded.</summary>
    public const int MaxLimit = 100;

    public int Limit { get; }
    public int Offset { get; }

    private PageRequest(int limit, int offset)
    {
        Limit = limit;
        Offset = offset;
    }

    /// <exception cref="ClinicalCareValidationException">The limit is outside 1 to <see cref="MaxLimit"/> or the offset is negative.</exception>
    public static PageRequest Create(int? limit, int? offset)
    {
        var errors = new List<string>();

        if (limit is < 1 or > MaxLimit)
            errors.Add($"Limit must be between 1 and {MaxLimit}.");

        if (offset is < 0)
            errors.Add("Offset cannot be negative.");

        if (errors.Count > 0)
            throw new ClinicalCareValidationException(errors);

        return new PageRequest(limit ?? DefaultLimit, offset ?? 0);
    }
}
