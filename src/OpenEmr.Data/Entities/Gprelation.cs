using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// general purpose relations
/// </summary>
public partial class Gprelation
{
    public int Type1 { get; set; }

    public long Id1 { get; set; }

    public int Type2 { get; set; }

    public long Id2 { get; set; }
}
