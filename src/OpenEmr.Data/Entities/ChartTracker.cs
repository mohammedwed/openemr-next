using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ChartTracker
{
    public int CtPid { get; set; }

    public DateTime CtWhen { get; set; }

    public long CtUserid { get; set; }

    public string CtLocation { get; set; } = null!;
}
