using OpenEmrNext.PatientManagement.Application.Ports;
using OpenEmrNext.PatientManagement.Domain.Insurance;
using OpenEmrNext.PatientManagement.Domain.Insurance.ValueObjects;
using OpenEmrNext.PatientManagement.Domain.Insurance.Rules;
using OpenEmrNext.PatientManagement.Domain.Insurance.ReadModels;

namespace OpenEmrNext.PatientManagement.Application;

public sealed class AddInsurancePolicyUseCase(IInsurancePolicyRepository repository)
{
    /// <exception cref="InsurancePolicyValidationException">One or more fields fail validation. Nothing is persisted.</exception>
    public async Task<InsurancePolicy> ExecuteAsync(
        long pid, InsuranceLevel level, long companyId, string policyNumber,
        string? groupNumber, string? planName, DateOnly startDate, DateOnly? endDate, CancellationToken ct)
    {
        var policy = InsurancePolicy.Add(pid, level, companyId, policyNumber, groupNumber, planName, startDate, endDate);
        await repository.AddAsync(policy, ct);
        return policy;
    }
}
