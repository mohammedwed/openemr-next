namespace OpenEmrNext.ClinicalCare.Application;

/// <summary>
/// The patient an encounter was requested for does not exist (CC-1). A missing thing is a not-found, not a
/// validation error, so the endpoint maps it to 404 rather than 400 (DOM-17).
/// </summary>
public sealed class PatientNotFoundException(Guid patientUuid)
    : Exception($"No patient with uuid {patientUuid} exists.")
{
    public Guid PatientUuid { get; } = patientUuid;
}
