using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Migration
{
    public string Version { get; set; } = null!;

    public DateTime? ExecutedAt { get; set; }

    public int? ExecutionDurationMs { get; set; }
}
