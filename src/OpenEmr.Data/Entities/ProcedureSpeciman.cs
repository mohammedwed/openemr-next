using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProcedureSpeciman
{
    /// <summary>
    /// record id
    /// </summary>
    public long ProcedureSpecimenId { get; set; }

    /// <summary>
    /// FHIR Specimen id
    /// </summary>
    public byte[]? Uuid { get; set; }

    /// <summary>
    /// links to procedure_order.procedure_order_id
    /// </summary>
    public long ProcedureOrderId { get; set; }

    /// <summary>
    /// links to procedure_order_code.procedure_order_seq (per test line)
    /// </summary>
    public int ProcedureOrderSeq { get; set; }

    /// <summary>
    /// tube/barcode/internal id
    /// </summary>
    public string? SpecimenIdentifier { get; set; }

    /// <summary>
    /// lab accession number
    /// </summary>
    public string? AccessionIdentifier { get; set; }

    /// <summary>
    /// prefer SNOMED CT code
    /// </summary>
    public string? SpecimenTypeCode { get; set; }

    /// <summary>
    /// display/text
    /// </summary>
    public string? SpecimenType { get; set; }

    public string? CollectionMethodCode { get; set; }

    public string? CollectionMethod { get; set; }

    public string? SpecimenLocationCode { get; set; }

    public string? SpecimenLocation { get; set; }

    /// <summary>
    /// single instant
    /// </summary>
    public DateTime? CollectedDate { get; set; }

    /// <summary>
    /// period start
    /// </summary>
    public DateTime? CollectionDateLow { get; set; }

    /// <summary>
    /// period end
    /// </summary>
    public DateTime? CollectionDateHigh { get; set; }

    public decimal? VolumeValue { get; set; }

    public string? VolumeUnit { get; set; }

    /// <summary>
    /// HL7 v2 0493 (e.g., ACT, HEM)
    /// </summary>
    public string? ConditionCode { get; set; }

    public string? SpecimenCondition { get; set; }

    public string? Comments { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }

    public bool? Deleted { get; set; }
}
