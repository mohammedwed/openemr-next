using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientTreatmentInterventionPreference
{
    public int Id { get; set; }

    public byte[]? Uuid { get; set; }

    /// <summary>
    /// fk to patient_data.pid
    /// </summary>
    public int PatientId { get; set; }

    /// <summary>
    /// LOINC code
    /// </summary>
    public string ObservationCode { get; set; } = null!;

    public string? ObservationCodeText { get; set; }

    public string? ValueType { get; set; }

    /// <summary>
    /// fk to preference_value_sets.answer_code
    /// </summary>
    public string? ValueCode { get; set; }

    /// <summary>
    /// fk to preference_value_sets.answer_system
    /// </summary>
    public string? ValueCodeSystem { get; set; }

    /// <summary>
    /// fk to preference_value_sets.answer_display
    /// </summary>
    public string? ValueDisplay { get; set; }

    public string? ValueText { get; set; }

    public bool? ValueBoolean { get; set; }

    public DateTime EffectiveDatetime { get; set; }

    /// <summary>
    /// valid options are final,amended,preliminary
    /// </summary>
    public string? Status { get; set; }

    public string? Note { get; set; }
}
