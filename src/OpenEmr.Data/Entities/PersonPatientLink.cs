using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Links person records to patient_data records when person becomes patient
/// </summary>
public partial class PersonPatientLink
{
    public long Id { get; set; }

    /// <summary>
    /// FK to person.id
    /// </summary>
    public long PersonId { get; set; }

    /// <summary>
    /// FK to patient_data.id
    /// </summary>
    public long PatientId { get; set; }

    /// <summary>
    /// When the link was created
    /// </summary>
    public DateTime LinkedDate { get; set; }

    /// <summary>
    /// FK to users.id - who created the link
    /// </summary>
    public long? LinkedBy { get; set; }

    /// <summary>
    /// How link was created: manual, auto_detected, migrated, import
    /// </summary>
    public string? LinkMethod { get; set; }

    /// <summary>
    /// Optional notes about why/how they were linked
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether link is active (allows soft delete)
    /// </summary>
    public bool? Active { get; set; }
}
