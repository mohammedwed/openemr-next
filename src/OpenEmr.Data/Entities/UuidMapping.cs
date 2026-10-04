using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class UuidMapping
{
    public long Id { get; set; }

    public byte[] Uuid { get; set; } = null!;

    public string Resource { get; set; } = null!;

    public string? ResourcePath { get; set; }

    public string Table { get; set; } = null!;

    public byte[] TargetUuid { get; set; } = null!;

    public DateTime? Created { get; set; }
}
