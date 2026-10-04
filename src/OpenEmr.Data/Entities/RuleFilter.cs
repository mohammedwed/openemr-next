using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class RuleFilter
{
    /// <summary>
    /// Maps to the id column in the clinical_rules table
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// 0 is exclude and 1 is include
    /// </summary>
    public bool IncludeFlag { get; set; }

    /// <summary>
    /// 0 is optional and 1 is required
    /// </summary>
    public bool RequiredFlag { get; set; }

    /// <summary>
    /// Maps to list_options list rule_filters
    /// </summary>
    public string Method { get; set; } = null!;

    /// <summary>
    /// Maps to list_options lists rule__intervals
    /// </summary>
    public string MethodDetail { get; set; } = null!;

    public string Value { get; set; } = null!;
}
