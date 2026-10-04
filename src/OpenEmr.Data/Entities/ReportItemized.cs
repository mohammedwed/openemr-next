using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ReportItemized
{
    public long ReportId { get; set; }

    public short ItemizedTestId { get; set; }

    /// <summary>
    /// Only used in special cases
    /// </summary>
    public string NumeratorLabel { get; set; } = null!;

    /// <summary>
    /// 0 is fail, 1 is pass, 2 is excluded
    /// </summary>
    public bool Pass { get; set; }

    public long Pid { get; set; }

    /// <summary>
    /// fk to clinical_rules.rule_id
    /// </summary>
    public string? RuleId { get; set; }

    /// <summary>
    /// JSON with specific sub item results for a clinical rule
    /// </summary>
    public string? ItemDetails { get; set; }
}
