namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>The signed-in user does not hold the right legacy's access control requires for this action. Maps to 403 (ADR 0076).</summary>
public sealed class AccessDeniedException(string right) : Exception($"The signed-in user does not hold {right}.")
{
    /// <summary>The right asked for, as legacy names it (<c>admin/super</c>). Safe to show: it names no record and no user.</summary>
    public string Right { get; } = right;
}

/// <summary>The record to delete does not exist in this encounter, or is already deleted. Maps to 404.</summary>
public sealed class RecordNotFoundException(string key) : Exception($"No such record {key} in this encounter.");

/// <summary>The record changed after the caller read it. Carries the current <c>ETag</c> so the caller can reload. Maps to 412.</summary>
public sealed class RecordVersionConflictException(string key, string? currentVersion) : Exception($"{key} changed after it was read.")
{
    public string? CurrentVersion { get; } = currentVersion;
}
