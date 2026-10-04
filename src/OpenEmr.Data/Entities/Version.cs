using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Version
{
    public int VMajor { get; set; }

    public int VMinor { get; set; }

    public int VPatch { get; set; }

    public int VRealpatch { get; set; }

    public string VTag { get; set; } = null!;

    public int VDatabase { get; set; }

    public int VAcl { get; set; }
}
