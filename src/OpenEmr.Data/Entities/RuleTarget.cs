using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class RuleTarget
{
    /// <summary>
    /// Maps to the id column in the clinical_rules table
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Contains group id to identify collection of targets in a rule
    /// </summary>
    public long GroupId { get; set; }

    /// <summary>
    /// 0 is exclude and 1 is include
    /// </summary>
    public bool IncludeFlag { get; set; }

    /// <summary>
    /// 0 is required and 1 is optional
    /// </summary>
    public bool RequiredFlag { get; set; }

    /// <summary>
    /// Maps to list_options list rule_targets
    /// </summary>
    public string Method { get; set; } = null!;

    /// <summary>
    /// Data is dependent on the method
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Only used in interval entries
    /// </summary>
    public long Interval { get; set; }
}
