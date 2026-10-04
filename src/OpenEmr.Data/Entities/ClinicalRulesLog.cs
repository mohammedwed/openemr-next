using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ClinicalRulesLog
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public long Pid { get; set; }

    public long Uid { get; set; }

    /// <summary>
    /// An example category is clinical_reminder_widget
    /// </summary>
    public string Category { get; set; } = null!;

    public string? Value { get; set; }

    public string? NewValue { get; set; }

    /// <summary>
    /// facility where the rule was executed, 0 if unknown
    /// </summary>
    public int? FacilityId { get; set; }
}
