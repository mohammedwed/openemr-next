using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FacilityUserId
{
    public long Id { get; set; }

    public long? Uid { get; set; }

    public long? FacilityId { get; set; }

    public byte[]? Uuid { get; set; }

    /// <summary>
    /// references layout_options.field_id
    /// </summary>
    public string FieldId { get; set; } = null!;

    public string? FieldValue { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime LastUpdated { get; set; }
}
