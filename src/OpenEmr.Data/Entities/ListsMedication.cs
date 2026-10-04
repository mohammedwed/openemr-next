using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Holds additional data about patient medications.
/// </summary>
public partial class ListsMedication
{
    public long Id { get; set; }

    /// <summary>
    /// FK Reference to lists.id
    /// </summary>
    public long? ListId { get; set; }

    /// <summary>
    /// Free text dosage instructions for taking the drug
    /// </summary>
    public string? DrugDosageInstructions { get; set; }

    /// <summary>
    /// option_id in list_options.list_id=medication-usage-category
    /// </summary>
    public string? UsageCategory { get; set; }

    /// <summary>
    /// title in list_options.list_id=medication-usage-category
    /// </summary>
    public string UsageCategoryTitle { get; set; } = null!;

    /// <summary>
    /// option_id in list_options.list_id=medication-request-intent
    /// </summary>
    public string? RequestIntent { get; set; }

    /// <summary>
    /// title in list_options.list_id=medication-request-intent
    /// </summary>
    public string RequestIntentTitle { get; set; } = null!;

    /// <summary>
    /// fk to list_options.option_id where list_id=medication_adherence_information_source to indicate who provided the medication adherence information
    /// </summary>
    public string? MedicationAdherenceInformationSource { get; set; }

    /// <summary>
    /// fk to list_options.option_id where list_id=medication_adherence to indicate if patient is complying with medication regimen
    /// </summary>
    public string? MedicationAdherence { get; set; }

    /// <summary>
    /// Date when the medication adherence information was asserted
    /// </summary>
    public DateTime? MedicationAdherenceDateAsserted { get; set; }

    /// <summary>
    /// fk to prescriptions.prescription_id to link medication to prescription record
    /// </summary>
    public long? PrescriptionId { get; set; }

    /// <summary>
    /// Indicates if this medication is a primary record(1) or a reported record(0)
    /// </summary>
    public bool? IsPrimaryRecord { get; set; }

    /// <summary>
    /// If this is a reported record, this is the fk to the users.id column for the address book user that the medication was reported by
    /// </summary>
    public long? ReportingSourceRecordId { get; set; }
}
