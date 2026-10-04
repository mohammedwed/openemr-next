using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class MedexRecall
{
    public int RId { get; set; }

    public int RPractid { get; set; }

    /// <summary>
    /// PatientID from pat_data
    /// </summary>
    public int RPid { get; set; }

    /// <summary>
    /// Date of Appt or Recall
    /// </summary>
    public DateOnly REventDate { get; set; }

    public int RFacility { get; set; }

    public int RProvider { get; set; }

    public string? RReason { get; set; }

    public DateTime RCreated { get; set; }
}
