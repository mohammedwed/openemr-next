using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;

namespace OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

/// <summary>One insurance policy in force for a patient on a day, at its level.</summary>
/// <param name="Start">The first day in force, or null when the start is blank or 0000-00-00 (always).</param>
/// <param name="End">The last day in force, or null when the end is blank or 0000-00-00 (open).</param>
public sealed record EffectiveInsurancePolicy(
    InsuranceLevel Level,
    long Id,
    Guid? Uuid,
    long CompanyId,
    DateOnly? Start,
    DateOnly? End);
