using Microsoft.EntityFrameworkCore;
using OpenEmrNext.ClinicalCare.Application.Ports;
using OpenEmrNext.ClinicalCare.Domain.ReadModels;
using OpenEmrNext.ClinicalCare.Domain.ValueObjects;

namespace OpenEmrNext.ClinicalCare.Infrastructure;

/// <summary>
/// The adapter side of <see cref="IImmunizationReader"/>: reads <c>immunizations</c> rows written by any legacy route, tolerantly (DOM-9, LEG-4). A row with no <c>uuid</c> is not addressable, so it is not returned
/// (ADR 0070 #6). The query text is a constant shared with the updater, which re-reads inside its lock so the version it checks is built by the same mapping a caller's was (ADR 0072).
/// </summary>
public sealed class EfImmunizationReader(ClinicalCareDbContext db) : IImmunizationReader
{
    /// <summary>The column list every read uses. A constant: no part of it is ever built from input.</summary>
    private const string Select = """
        SELECT i.uuid AS Uuid, pr.patient_uuid AS PatientUuid, i.patient_id AS Pid, i.administered_date AS AdministeredDate, i.immunization_id AS ImmunizationId,
               i.cvx_code AS CvxCode, i.manufacturer AS Manufacturer, i.lot_number AS LotNumber, i.expiration_date AS ExpirationDate,
               i.amount_administered AS Amount, i.amount_administered_unit AS AmountUnit, i.route AS Route, i.administration_site AS AdministrationSite,
               i.completion_status AS CompletionStatus, i.information_source AS InformationSource, i.refusal_reason AS RefusalReason,
               i.education_date AS EducationDate, i.vis_date AS VisDate, i.administered_by AS AdministeredBy, i.administered_by_id AS AdministeredById,
               i.ordering_provider AS OrderingProvider, i.encounter_id AS EncounterNumber, i.reason_code AS ReasonCode,
               i.reason_description AS ReasonDescription, i.note AS Note, i.added_erroneously AS EnteredInError, i.create_date AS RecordedAt
        FROM immunizations i
        LEFT JOIN clinical_care_patient_ref pr ON pr.pid = i.patient_id
        """;

    public async Task<ImmunizationView?> FindAsync(Guid immunizationUuid, CancellationToken ct) => await FindAsync(db, immunizationUuid, ct);

    internal static async Task<ImmunizationView?> FindAsync(ClinicalCareDbContext db, Guid immunizationUuid, CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQueryRaw<ImmunizationRow>(Select + " WHERE i.uuid = {0}", Rfc4122GuidConverter.ToRfc4122Bytes(immunizationUuid))
            .ToListAsync(ct);
        return rows.Select(ToView).FirstOrDefault();
    }

    public async Task<ImmunizationPage> ListForPatientAsync(PatientReference patient, PageRequest page, CancellationToken ct)
    {
        var rows = await db.Database
            .SqlQueryRaw<ImmunizationRow>(
                Select + " WHERE i.patient_id = {0} AND i.uuid IS NOT NULL ORDER BY i.administered_date DESC, i.id DESC LIMIT {1} OFFSET {2}",
                patient.Pid, page.Limit, page.Offset)
            .ToListAsync(ct);

        var total = await db.Database
            .SqlQuery<long>($"SELECT COUNT(*) AS Value FROM immunizations WHERE patient_id = {patient.Pid} AND uuid IS NOT NULL")
            .SingleAsync(ct);

        return new ImmunizationPage(rows.Select(ToView).ToList(), (int)total);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static ImmunizationView ToView(ImmunizationRow r) => new(
        Rfc4122GuidConverter.FromRfc4122Bytes(r.Uuid!),
        r.PatientUuid is { Length: 16 } patient ? Rfc4122GuidConverter.FromRfc4122Bytes(patient) : null,
        r.Pid,
        r.AdministeredDate,
        r.ImmunizationId is > 0 ? r.ImmunizationId : null,
        Blank(r.CvxCode), Blank(r.Manufacturer), Blank(r.LotNumber), r.ExpirationDate,
        // The column is a 4-byte float: read it through decimal at four places, the precision the service writes, so the same row always reads the same.
        r.Amount is { } amount and > 0 ? Math.Round((decimal)amount, 4, MidpointRounding.AwayFromZero) : null,
        Blank(r.AmountUnit), Blank(r.Route), Blank(r.AdministrationSite), Blank(r.CompletionStatus), Blank(r.InformationSource), Blank(r.RefusalReason),
        r.EducationDate, r.VisDate, Blank(r.AdministeredBy), r.AdministeredById is > 0 ? r.AdministeredById : null,
        r.OrderingProvider is > 0 ? r.OrderingProvider : null, r.EncounterNumber is > 0 ? r.EncounterNumber : null,
        Blank(r.ReasonCode), Blank(r.ReasonDescription), Blank(r.Note), r.EnteredInError, r.RecordedAt);

    private sealed class ImmunizationRow
    {
        public byte[]? Uuid { get; set; }
        public byte[]? PatientUuid { get; set; }
        public long? Pid { get; set; }
        public DateTime? AdministeredDate { get; set; }
        public int? ImmunizationId { get; set; }
        public string? CvxCode { get; set; }
        public string? Manufacturer { get; set; }
        public string? LotNumber { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public float? Amount { get; set; }
        public string? AmountUnit { get; set; }
        public string? Route { get; set; }
        public string? AdministrationSite { get; set; }
        public string? CompletionStatus { get; set; }
        public string? InformationSource { get; set; }
        public string? RefusalReason { get; set; }
        public DateTime? EducationDate { get; set; }
        public DateTime? VisDate { get; set; }
        public string? AdministeredBy { get; set; }
        public long? AdministeredById { get; set; }
        public long? OrderingProvider { get; set; }
        public long? EncounterNumber { get; set; }
        public string? ReasonCode { get; set; }
        public string? ReasonDescription { get; set; }
        public string? Note { get; set; }
        public bool EnteredInError { get; set; }
        public DateTime? RecordedAt { get; set; }
    }
}
