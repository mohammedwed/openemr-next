using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Main calculation records - one per logical calculation (e.g., average BP)
/// </summary>
public partial class FormVitalsCalculation
{
    public int Id { get; set; }

    public byte[]? Uuid { get; set; }

    /// <summary>
    /// fk to form_encounter.id
    /// </summary>
    public long? Encounter { get; set; }

    /// <summary>
    /// fk to patient_data.pid
    /// </summary>
    public long Pid { get; set; }

    public DateTime? DateStart { get; set; }

    public DateTime? DateEnd { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }

    /// <summary>
    /// application identifier representing calculation e.g., bp-MeanLast5, bp-Mean3Day, bp-MeanEncounter
    /// </summary>
    public string? CalculationId { get; set; }
}
