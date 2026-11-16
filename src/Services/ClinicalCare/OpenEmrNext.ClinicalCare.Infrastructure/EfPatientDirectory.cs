using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IPatientDirectory"/>: answers "does this patient exist?" from Clinical Care's **own**
/// <c>clinical_care_patient_ref</c> table, filled by <c>PatientRegistered</c> events (and the provisioning backfill), never by
/// reading another service's table. This replaced the interim <c>patient_data</c> stand-in on the day events arrived
/// (ADR 0064 #5, ADR 0066): the grant on <c>patient_data</c> was revoked and the stand-in's tests deleted.
/// </summary>
public sealed class EfPatientDirectory(ClinicalCareDbContext db) : IPatientDirectory
{
    public async Task<PatientReference?> FindAsync(Guid patientUuid, CancellationToken ct)
    {
        var uuidBytes = Rfc4122GuidConverter.ToRfc4122Bytes(patientUuid);

        var rows = await db.Database
            .SqlQuery<PatientRow>($"SELECT pid AS Pid FROM clinical_care_patient_ref WHERE patient_uuid = {uuidBytes}")
            .ToListAsync(ct);

        // pid is NOT NULL in our own table, so a non-positive value can only be a bad event; treat it as unknown rather than
        // handing out a reference that legacy tables cannot join on (DOM-9).
        var row = rows.FirstOrDefault();
        return row is { Pid: > 0 } ? PatientReference.Create(patientUuid, row.Pid) : null;
    }

    private sealed class PatientRow
    {
        public long Pid { get; set; }
    }
}
