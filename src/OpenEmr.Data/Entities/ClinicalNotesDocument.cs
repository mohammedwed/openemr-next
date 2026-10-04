using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Links clinical notes to patient documents
/// </summary>
public partial class ClinicalNotesDocument
{
    public long Id { get; set; }

    /// <summary>
    /// Foreign key to form_clinical_notes.id
    /// </summary>
    public long ClinicalNoteId { get; set; }

    /// <summary>
    /// Foreign key to documents.id
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// When the link was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Username who created the link
    /// </summary>
    public string? CreatedBy { get; set; }
}
