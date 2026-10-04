using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OpenemrModuleVar
{
    public uint PnId { get; set; }

    public string? PnModname { get; set; }

    public string? PnName { get; set; }

    public string? PnValue { get; set; }
}
