using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Component values for calculations (e.g., systolic=120, diastolic=80)
/// </summary>
public partial class FormVitalsCalculationComponent
{
    public int Id { get; set; }

    /// <summary>
    /// fk to form_vitals_calculation.uuid
    /// </summary>
    public byte[] FvcUuid { get; set; } = null!;

    /// <summary>
    /// Component type: bps, bpd, pulse, etc.
    /// </summary>
    public string VitalsColumn { get; set; } = null!;

    /// <summary>
    /// Calculated numeric component value
    /// </summary>
    public decimal? Value { get; set; }

    /// <summary>
    /// Calculated non-numeric component value
    /// </summary>
    public string? ValueString { get; set; }

    /// <summary>
    /// Unit for this component value
    /// </summary>
    public string? ValueUnit { get; set; }

    /// <summary>
    /// Display order for components
    /// </summary>
    public int ComponentOrder { get; set; }
}
