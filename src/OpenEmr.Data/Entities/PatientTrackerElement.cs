using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientTrackerElement
{
    /// <summary>
    /// maps to id column in patient_tracker table
    /// </summary>
    public long PtTrackerId { get; set; }

    public DateTime? StartDatetime { get; set; }

    public string Room { get; set; } = null!;

    public string Status { get; set; } = null!;

    /// <summary>
    /// This is a numerical sequence for this pt_tracker_id events
    /// </summary>
    public string Seq { get; set; } = null!;

    /// <summary>
    /// This is the user that created this element
    /// </summary>
    public string User { get; set; } = null!;
}
