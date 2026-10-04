using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class LayoutGroupProperty
{
    public string GrpFormId { get; set; } = null!;

    /// <summary>
    /// empty when representing the whole form
    /// </summary>
    public string GrpGroupId { get; set; } = null!;

    /// <summary>
    /// descriptive name of the form or group
    /// </summary>
    public string GrpTitle { get; set; } = null!;

    /// <summary>
    /// for display under the title
    /// </summary>
    public string GrpSubtitle { get; set; } = null!;

    /// <summary>
    /// the form category
    /// </summary>
    public string GrpMapping { get; set; } = null!;

    /// <summary>
    /// optional order within mapping
    /// </summary>
    public int GrpSeq { get; set; }

    public bool? GrpActivity { get; set; }

    public int GrpRepeats { get; set; }

    public int GrpColumns { get; set; }

    public int GrpSize { get; set; }

    public string GrpIssueType { get; set; } = null!;

    public string GrpAcoSpec { get; set; } = null!;

    public bool GrpSaveClose { get; set; }

    public bool GrpInitOpen { get; set; }

    public bool GrpReferrals { get; set; }

    public bool GrpUnchecked { get; set; }

    public string GrpServices { get; set; } = null!;

    public string GrpProducts { get; set; } = null!;

    public string GrpDiags { get; set; } = null!;

    public DateTime? GrpLastUpdate { get; set; }
}
