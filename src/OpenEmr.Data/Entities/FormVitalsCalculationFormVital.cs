using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Join table between form_vitals_calculation and form_vitals table representing the derivative observation relationship between the calculation and the source records
/// </summary>
public partial class FormVitalsCalculationFormVital
{
    /// <summary>
    /// fk to form_vitals_calculation.uuid
    /// </summary>
    public byte[] FvcUuid { get; set; } = null!;

    /// <summary>
    /// fk to form_vitals.id
    /// </summary>
    public long VitalsId { get; set; }
}
