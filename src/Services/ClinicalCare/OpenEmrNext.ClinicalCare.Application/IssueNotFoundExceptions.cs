namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// The problem to update does not exist, or its uuid belongs to a row of another issue type (ADR 0070 #2). A missing thing is a not-found, not a validation
/// error, so the endpoint maps it to 404 (DOM-17).
/// </summary>
public sealed class ProblemNotFoundException(Guid problemUuid) : Exception($"No problem with uuid {problemUuid} exists.")
{
    public Guid ProblemUuid { get; } = problemUuid;
}

/// <summary>The allergy to update does not exist, or its uuid belongs to a row of another issue type (ADR 0070 #2). Maps to 404 (DOM-17).</summary>
public sealed class AllergyNotFoundException(Guid allergyUuid) : Exception($"No allergy with uuid {allergyUuid} exists.")
{
    public Guid AllergyUuid { get; } = allergyUuid;
}

/// <summary>The issue to link or unlink does not exist, or its uuid belongs to a row that is not a problem or an allergy (ADR 0070 #2). Maps to 404 (DOM-17).</summary>
public sealed class IssueNotFoundException(Guid issueUuid) : Exception($"No problem or allergy with uuid {issueUuid} exists.")
{
    public Guid IssueUuid { get; } = issueUuid;
}
