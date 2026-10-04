using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ListsTouch
{
    public long Pid { get; set; }

    public string Type { get; set; } = null!;

    public DateTime? Date { get; set; }
}
