using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OpenemrModule
{
    public uint PnId { get; set; }

    public string? PnName { get; set; }

    public int PnType { get; set; }

    public string? PnDisplayname { get; set; }

    public string? PnDescription { get; set; }

    public uint PnRegid { get; set; }

    public string? PnDirectory { get; set; }

    public string? PnVersion { get; set; }

    public bool PnAdminCapable { get; set; }

    public bool PnUserCapable { get; set; }

    public bool PnState { get; set; }
}
