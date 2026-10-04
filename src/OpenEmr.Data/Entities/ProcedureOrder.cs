using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProcedureOrder
{
    public long ProcedureOrderId { get; set; }

    public byte[]? Uuid { get; set; }

    /// <summary>
    /// references users.id, the ordering provider
    /// </summary>
    public long ProviderId { get; set; }

    /// <summary>
    /// references patient_data.pid
    /// </summary>
    public long PatientId { get; set; }

    /// <summary>
    /// references form_encounter.encounter
    /// </summary>
    public long EncounterId { get; set; }

    /// <summary>
    /// time specimen collected
    /// </summary>
    public DateTime? DateCollected { get; set; }

    public DateTime? DateOrdered { get; set; }

    public string OrderPriority { get; set; } = null!;

    /// <summary>
    /// pending,routed,complete,canceled
    /// </summary>
    public string OrderStatus { get; set; } = null!;

    public string? PatientInstructions { get; set; }

    /// <summary>
    /// 0 if deleted
    /// </summary>
    public bool? Activity { get; set; }

    /// <summary>
    /// This is the CONTROL ID that is sent back from lab
    /// </summary>
    public string ControlId { get; set; } = null!;

    /// <summary>
    /// references procedure_providers.ppid
    /// </summary>
    public long LabId { get; set; }

    /// <summary>
    /// from the Specimen_Type list
    /// </summary>
    public string SpecimenType { get; set; } = null!;

    /// <summary>
    /// from the Specimen_Location list
    /// </summary>
    public string SpecimenLocation { get; set; } = null!;

    /// <summary>
    /// from a text input field
    /// </summary>
    public string SpecimenVolume { get; set; } = null!;

    /// <summary>
    /// time of order transmission, null if unsent
    /// </summary>
    public DateTime? DateTransmitted { get; set; }

    /// <summary>
    /// clinical history text that may be relevant to the order
    /// </summary>
    public string ClinicalHx { get; set; } = null!;

    public string? ExternalId { get; set; }

    /// <summary>
    /// references order is added for history purpose only.
    /// </summary>
    public string? HistoryOrder { get; set; }

    /// <summary>
    /// primary order diagnosis
    /// </summary>
    public string? OrderDiagnosis { get; set; }

    public string? BillingType { get; set; }

    public string? SpecimenFasting { get; set; }

    public sbyte? OrderPsc { get; set; }

    public string OrderAbn { get; set; } = null!;

    public long CollectorId { get; set; }

    public string? Account { get; set; }

    public int? AccountFacility { get; set; }

    public string? ProviderNumber { get; set; }

    public string ProcedureOrderType { get; set; } = null!;

    /// <summary>
    /// Scheduled date for service (FHIR occurrence[x])
    /// </summary>
    public DateTime? ScheduledDate { get; set; }

    /// <summary>
    /// Scheduled start time (FHIR occurrencePeriod.start)
    /// </summary>
    public DateTime? ScheduledStart { get; set; }

    /// <summary>
    /// Scheduled end time (FHIR occurrencePeriod.end)
    /// </summary>
    public DateTime? ScheduledEnd { get; set; }

    /// <summary>
    /// Type of performer: laboratory, radiology, pathology (SNOMED CT)
    /// </summary>
    public string? PerformerType { get; set; }

    /// <summary>
    /// FHIR intent: order, plan, directive, proposal
    /// </summary>
    public string OrderIntent { get; set; } = null!;

    /// <summary>
    /// References facility.id for service location (FHIR locationReference)
    /// </summary>
    public int? LocationId { get; set; }
}
