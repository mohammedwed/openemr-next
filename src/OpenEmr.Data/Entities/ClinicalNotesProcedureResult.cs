using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Links clinical notes to procedure results/lab values
/// </summary>
public partial class ClinicalNotesProcedureResult
{
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to form_clinical_notes.id
    /// </summary>
    public long ClinicalNoteId { get; set; }

    /// <summary>
    /// Foreign key to procedure_result.procedure_result_id
    /// </summary>
    public long ProcedureResultId { get; set; }

    /// <summary>
    /// When the link was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Username who created the link
    /// </summary>
    public string? CreatedBy { get; set; }
}
