using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class SharedAttribute
{
    public long Pid { get; set; }

    /// <summary>
    /// 0 if patient attribute, else encounter attribute
    /// </summary>
    public long Encounter { get; set; }

    /// <summary>
    /// references layout_options.field_id
    /// </summary>
    public string FieldId { get; set; } = null!;

    /// <summary>
    /// time of last update
    /// </summary>
    public DateTime LastUpdate { get; set; }

    /// <summary>
    /// user who last updated
    /// </summary>
    public long UserId { get; set; }

    public string? FieldValue { get; set; }
}
