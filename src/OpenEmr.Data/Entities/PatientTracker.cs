using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class PatientTracker
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public DateOnly? Apptdate { get; set; }

    public TimeOnly? Appttime { get; set; }

    public long Eid { get; set; }

    public long Pid { get; set; }

    /// <summary>
    /// This is the user that created the original record
    /// </summary>
    public string OriginalUser { get; set; } = null!;

    public long Encounter { get; set; }

    /// <summary>
    /// The element file should contain this number of elements
    /// </summary>
    public string Lastseq { get; set; } = null!;

    /// <summary>
    /// NULL if not randomized. If randomized, 0 is no, 1 is yes
    /// </summary>
    public bool? RandomDrugTest { get; set; }

    public bool DrugScreenCompleted { get; set; }
}
