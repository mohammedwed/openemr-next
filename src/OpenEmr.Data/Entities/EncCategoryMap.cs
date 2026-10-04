using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class EncCategoryMap
{
    /// <summary>
    /// encounter id from rule_enc_types list in list_options
    /// </summary>
    public string RuleEncId { get; set; } = null!;

    /// <summary>
    /// category id from event category in openemr_postcalendar_categories
    /// </summary>
    public int MainCatId { get; set; }
}
