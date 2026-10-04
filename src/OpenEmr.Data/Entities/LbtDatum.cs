using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// contains all data from layout-based transactions
/// </summary>
public partial class LbtDatum
{
    /// <summary>
    /// references transactions.id
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// references layout_options.field_id
    /// </summary>
    public string FieldId { get; set; } = null!;

    public string? FieldValue { get; set; }
}
