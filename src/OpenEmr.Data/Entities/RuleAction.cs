using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class RuleAction
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
    /// Maps to the category item in the rule_action_item table
    /// </summary>
    public string Category { get; set; } = null!;

    /// <summary>
    /// Maps to the item column in the rule_action_item table
    /// </summary>
    public string Item { get; set; } = null!;
}
