namespace OpenEmrNext.PatientManagement.Domain.Insurance;

/// <summary>
/// One insurance policy for a patient, at a level (<see cref="InsuranceLevel"/>). <see cref="Id"/> is
/// database-generated and stays unset until <see cref="AssignGeneratedIdentity"/> runs after insert,
/// the same pattern <c>Patient</c> uses.
/// </summary>
/// <remarks>
/// Validates only the fields legacy OpenEMR's own <c>CoverageValidator::DATABASE_INSERT_CONTEXT</c>
/// requires that don't depend on OpenEMR's own configured option lists (state, country, sex,
/// subscriber relationship) -- this service has no source of truth for those lists. Legacy also
/// requires a full set of subscriber demographics (name, DOB, address, sex) and
/// <c>accept_assignment</c>; deliberately not validated here, a scoped-down first cut, not an
/// oversight. Revisit if a real need for subscriber-demographic validation shows up.
/// </remarks>
public sealed class InsurancePolicy
{
    public long Id { get; private set; }
    public Guid Uuid { get; }
    public long Pid { get; }
    public InsuranceLevel Level { get; }
    public long CompanyId { get; }
    public string PolicyNumber { get; } = null!;
    public string? GroupNumber { get; }
    public string? PlanName { get; }
    public DateOnly StartDate { get; }
    public DateOnly? EndDate { get; }

    private InsurancePolicy(
        long pid, InsuranceLevel level, long companyId, string policyNumber,
        string? groupNumber, string? planName, DateOnly startDate, DateOnly? endDate)
    {
        Pid = pid;
        Level = level;
        CompanyId = companyId;
        PolicyNumber = policyNumber;
        GroupNumber = groupNumber;
        PlanName = planName;
        StartDate = startDate;
        EndDate = endDate;
        Uuid = Guid.NewGuid();
    }

    /// <exception cref="InsurancePolicyValidationException">One or more fields fail validation.</exception>
    public static InsurancePolicy Add(
        long pid, InsuranceLevel level, long companyId, string policyNumber,
        string? groupNumber, string? planName, DateOnly startDate, DateOnly? endDate)
    {
        var errors = new List<string>();

        if (pid <= 0)
            errors.Add("Pid must identify an existing patient.");

        if (companyId <= 0)
            errors.Add("Company must be a valid insurance company id.");

        if (policyNumber.Length is < 2 or > 255)
            errors.Add("Policy number must be between 2 and 255 characters.");

        if (groupNumber is { Length: > 0 } g && g.Length is < 2 or > 255)
            errors.Add("Group number must be between 2 and 255 characters.");

        if (planName is { Length: > 0 } p && p.Length is < 2 or > 255)
            errors.Add("Plan name must be between 2 and 255 characters.");

        if (endDate is { } end && end < startDate)
            errors.Add("End date cannot be before the start date.");

        if (errors.Count > 0)
            throw new InsurancePolicyValidationException(errors);

        return new InsurancePolicy(pid, level, companyId, policyNumber, groupNumber, planName, startDate, endDate);
    }

    internal void AssignGeneratedIdentity(long id)
    {
        if (Id != 0)
            throw new InvalidOperationException($"Insurance policy {Uuid} already has an assigned identity.");

        Id = id;
    }
}
