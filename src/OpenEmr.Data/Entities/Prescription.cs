using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Prescription
{
    public int Id { get; set; }

    public byte[]? Uuid { get; set; }

    public long? PatientId { get; set; }

    public int? FilledById { get; set; }

    public int? PharmacyId { get; set; }

    /// <summary>
    /// Datetime the prescriptions was initially created
    /// </summary>
    public DateTime? DateAdded { get; set; }

    /// <summary>
    /// Datetime the prescriptions was last modified
    /// </summary>
    public DateTime? DateModified { get; set; }

    public int? ProviderId { get; set; }

    public int? Encounter { get; set; }

    public DateOnly? StartDate { get; set; }

    public string? Drug { get; set; }

    public int DrugId { get; set; }

    public string? RxnormDrugcode { get; set; }

    public int? Form { get; set; }

    public string? Dosage { get; set; }

    public string? Quantity { get; set; }

    public string? Size { get; set; }

    public int? Unit { get; set; }

    /// <summary>
    /// Max size 100 characters is same max as immunizations
    /// </summary>
    public string? Route { get; set; }

    public int? Interval { get; set; }

    public int? Substitute { get; set; }

    public int? Refills { get; set; }

    public int? PerRefill { get; set; }

    public DateOnly? FilledDate { get; set; }

    public int? Medication { get; set; }

    public string? Note { get; set; }

    public int Active { get; set; }

    public DateTime? Datetime { get; set; }

    public string? User { get; set; }

    public string? Site { get; set; }

    public string? Prescriptionguid { get; set; }

    /// <summary>
    /// 0-OpenEMR 1-External
    /// </summary>
    public sbyte ErxSource { get; set; }

    /// <summary>
    /// 0-Pending NewCrop upload 1-Uploaded to NewCrop
    /// </summary>
    public sbyte ErxUploaded { get; set; }

    public string? DrugInfoErx { get; set; }

    public string? ExternalId { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Indication { get; set; }

    public string? Prn { get; set; }

    public int? Ntx { get; set; }

    public int? Rtx { get; set; }

    public DateOnly TxDate { get; set; }

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
    /// Medication dosage instructions
    /// </summary>
    public string? DrugDosageInstructions { get; set; }

    /// <summary>
    /// Diagnosis or reason for the prescription
    /// </summary>
    public string? Diagnosis { get; set; }

    /// <summary>
    /// users.id the user that first created this record
    /// </summary>
    public long? CreatedBy { get; set; }

    /// <summary>
    /// users.id the user that last modified this record
    /// </summary>
    public long? UpdatedBy { get; set; }
}
