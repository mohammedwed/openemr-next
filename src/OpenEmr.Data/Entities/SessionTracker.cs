using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class SessionTracker
{
    public byte[] Uuid { get; set; } = null!;

    public DateTime? Created { get; set; }

    public DateTime? LastUpdated { get; set; }

    public long? NumberScripts { get; set; }
}
