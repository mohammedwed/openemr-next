using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>Reads problems without touching an aggregate (ADR 0054). Every statement is scoped to <c>type = 'medical_problem'</c> (ADR 0070 #2).</summary>
public interface IProblemReader
{
    /// <returns>The problem, or <c>null</c> when none has this uuid, or the row is of another type.</returns>
    Task<ProblemView?> FindAsync(Guid problemUuid, CancellationToken ct);

    /// <returns>One bounded page of the patient's problems, newest first by when they began, with the total.</returns>
    Task<ProblemPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct);
}
