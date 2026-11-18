using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

/// <summary>
/// Adds a policy through the <see cref="InsuranceCoverage"/> cluster, not <see cref="InsurancePolicy.Add"/>
/// directly -- so the cluster-wide "no two policies at the same level and start date" invariant
/// (<see cref="InsuranceCoverage.AddPolicy"/>) is enforced here the same way it already was on the swap
/// path, instead of only ever being reachable through <see cref="SwapInsuranceUseCase"/>.
/// </summary>
public sealed class AddInsurancePolicyUseCase(IInsurancePolicyRepository repository, IAuditLog auditLog)
{
    public async Task<InsurancePolicy> ExecuteAsync(
        long pid, InsuranceLevel level, long companyId, string policyNumber,
        string? groupNumber, string? planName, DateOnly startDate, DateOnly? endDate, CancellationToken ct, AuditOrigin? origin = null)
    {
        InsurancePolicy policy;
        try
        {
            var existingPolicies = await repository.GetPoliciesAsync(pid, ct);
            var coverage = InsuranceCoverage.For(pid, existingPolicies);
            policy = coverage.AddPolicy(level, companyId, policyNumber, groupNumber, planName, startDate, endDate);
        }
        catch (InsurancePolicyValidationException)
        {
            await auditLog.AppendAsync(Audit.Failure(origin, AuditActions.InsuranceAdd, Audit.Patient(pid)), ct);
            throw;
        }

        await repository.AddAsync(policy, Audit.Success(origin, AuditActions.InsuranceAdd, policy.Uuid.ToString()), ct);
        return policy;
    }
}
