using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Application.Ports;

/// <summary>
/// Answers "does this patient exist, and what is their reference?" without Clinical Care owning patient data
/// (CC-1, design set R7).
/// </summary>
/// <remarks>
/// <b>Stand-in (ADR 0064 #5, MIG-11):</b> until slice 3, the adapter does a read-only lookup of
/// <c>patient_data.uuid</c>/<c>pid</c>. <b>Removal criterion:</b> slice 3 is done and
/// <c>clinical_care_patient_ref</c> is populated from <c>PatientRegistered</c> events plus a one-time
/// provisioning backfill. Then the adapter, its <c>patient_data</c> grant and its tests are deleted. The port
/// stays; only what stands behind it changes.
/// </remarks>
public interface IPatientDirectory
{
    /// <returns>The patient's reference, or <c>null</c> when no such patient exists.</returns>
    Task<PatientReference?> FindAsync(Guid patientUuid, CancellationToken ct);
}
