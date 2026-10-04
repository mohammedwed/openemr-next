using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ClinicalPlansRule
{
    /// <summary>
    /// Unique and maps to list_options list clinical_plans
    /// </summary>
    public string PlanId { get; set; } = null!;

    /// <summary>
    /// Unique and maps to list_options list clinical_rules
    /// </summary>
    public string RuleId { get; set; } = null!;
}
