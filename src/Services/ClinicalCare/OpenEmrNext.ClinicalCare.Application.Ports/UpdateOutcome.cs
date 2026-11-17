namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>How an update attempt on a shared legacy row ended (encounters, problems and allergies alike, ADR 0068).</summary>
public enum UpdateStatus
{
    /// <summary>The row, its event and its audit row were committed.</summary>
    Updated,

    /// <summary>The row exists but is no longer the version the caller read. Nothing was written.</summary>
    Conflict,

    /// <summary>No such record of the aggregate's type, or legacy deleted it. Nothing was written.</summary>
    NotFound
}

/// <param name="Status">How the attempt ended.</param>
/// <param name="Stamp">For <see cref="UpdateStatus.Updated"/>, the row's new stamp; for <see cref="UpdateStatus.Conflict"/>, its current one.</param>
public sealed record UpdateOutcome(UpdateStatus Status, DateTime? Stamp);

/// <summary>How a content-versioned update (ADR 0072) ended. <paramref name="Version"/> is the row's new version after <see cref="UpdateStatus.Updated"/>, or its current one after <see cref="UpdateStatus.Conflict"/>.</summary>
public sealed record ContentUpdateOutcome(UpdateStatus Status, OpenEmrNext.ClinicalCare.Domain.ValueObjects.ContentVersion? Version);
