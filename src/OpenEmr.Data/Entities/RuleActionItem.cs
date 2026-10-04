using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class RuleActionItem
{
    /// <summary>
    /// Maps to list_options list rule_action_category
    /// </summary>
    public string Category { get; set; } = null!;

    /// <summary>
    /// Maps to list_options list rule_action
    /// </summary>
    public string Item { get; set; } = null!;

    /// <summary>
    /// Custom html link in clinical reminder widget
    /// </summary>
    public string ClinRemLink { get; set; } = null!;

    /// <summary>
    /// Custom message in patient reminder
    /// </summary>
    public string? ReminderMessage { get; set; }

    /// <summary>
    /// 1 indexed to rule_patient_data, 0 indexed within main schema
    /// </summary>
    public bool CustomFlag { get; set; }
}
