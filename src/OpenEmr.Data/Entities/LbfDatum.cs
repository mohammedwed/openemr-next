using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// contains all data from layout-based forms
/// </summary>
public partial class LbfDatum
{
    /// <summary>
    /// references forms.form_id
    /// </summary>
    public int FormId { get; set; }

    /// <summary>
    /// references layout_options.field_id
    /// </summary>
    public string FieldId { get; set; } = null!;

    public string? FieldValue { get; set; }
}
