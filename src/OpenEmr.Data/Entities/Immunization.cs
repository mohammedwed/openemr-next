using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Immunization
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    public long? PatientId { get; set; }

    public DateTime? AdministeredDate { get; set; }

    public int? ImmunizationId { get; set; }

    public string? CvxCode { get; set; }

    public string? Manufacturer { get; set; }

    public string? LotNumber { get; set; }

    public long? AdministeredById { get; set; }

    /// <summary>
    /// Alternative to administered_by_id
    /// </summary>
    public string? AdministeredBy { get; set; }

    public DateOnly? EducationDate { get; set; }

    /// <summary>
    /// Date of VIS Statement
    /// </summary>
    public DateOnly? VisDate { get; set; }

    public string? Note { get; set; }

    public DateTime? CreateDate { get; set; }

    public DateTime UpdateDate { get; set; }

    public long? CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }

    public float? AmountAdministered { get; set; }

    public string? AmountAdministeredUnit { get; set; }

    public DateOnly? ExpirationDate { get; set; }

    public string? Route { get; set; }

    public string? AdministrationSite { get; set; }

    public bool AddedErroneously { get; set; }

    public string? ExternalId { get; set; }

    public string? CompletionStatus { get; set; }

    public string? InformationSource { get; set; }

    public string? RefusalReason { get; set; }

    public int? OrderingProvider { get; set; }

    /// <summary>
    /// Medical code explaining reason of the vital observation value in form codesystem:codetype;...;
    /// </summary>
    public string? ReasonCode { get; set; }

    /// <summary>
    /// Human readable text description of the reason_code column
    /// </summary>
    public string? ReasonDescription { get; set; }

    /// <summary>
    /// fk to form_encounter.encounter to link immunization to encounter record
    /// </summary>
    public long? EncounterId { get; set; }
}
