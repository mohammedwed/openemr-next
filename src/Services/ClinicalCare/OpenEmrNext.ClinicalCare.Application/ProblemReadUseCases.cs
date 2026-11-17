using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>Reads one problem by its uuid.</summary>
public sealed class GetProblemUseCase(IProblemReader reader)
{
    /// <returns>The problem, or <c>null</c> when there is none (or it is of another issue type: ADR 0070 #2).</returns>
    public Task<ProblemView?> ExecuteAsync(Guid problemUuid, CancellationToken ct) => reader.FindAsync(problemUuid, ct);
}

/// <summary>Lists one patient's problems, newest first, one bounded page at a time (API-6).</summary>
public sealed class ListPatientProblemsUseCase(IPatientDirectory patients, IProblemReader reader)
{
    /// <exception cref="ClinicalCareValidationException">The page bounds are outside what is allowed.</exception>
    /// <exception cref="PatientNotFoundException">No patient has this uuid.</exception>
    public async Task<ProblemPage> ExecuteAsync(Guid patientUuid, int? limit, int? offset, CancellationToken ct)
    {
        // Bounds first: a caller asking for 10,000 rows is told so without a database round trip.
        var page = PageRequest.Create(limit, offset);

        var patient = await patients.FindAsync(patientUuid, ct)
            ?? throw new PatientNotFoundException(patientUuid);

        return await reader.ListForPatientAsync(patient, page, ct);
    }
}
