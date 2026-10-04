using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class RulePatientDatum
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    public long Pid { get; set; }

    /// <summary>
    /// Maps to the category item in the rule_action_item table
    /// </summary>
    public string Category { get; set; } = null!;

    /// <summary>
    /// Maps to the item column in the rule_action_item table
    /// </summary>
    public string Item { get; set; } = null!;

    /// <summary>
    /// Maps to list_options list yesno
    /// </summary>
    public string Complete { get; set; } = null!;

    public string Result { get; set; } = null!;
}
