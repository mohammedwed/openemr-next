using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class RuleReminder
{
    /// <summary>
    /// Maps to the id column in the clinical_rules table
    /// </summary>
    public string Id { get; set; } = null!;

    /// <summary>
    /// Maps to list_options list rule_reminder_methods
    /// </summary>
    public string Method { get; set; } = null!;

    /// <summary>
    /// Maps to list_options list rule_reminder_intervals
    /// </summary>
    public string MethodDetail { get; set; } = null!;

    public string Value { get; set; } = null!;
}
