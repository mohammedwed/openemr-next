using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OnsiteOnline
{
    public string Hash { get; set; } = null!;

    public string Ip { get; set; } = null!;

    public DateTime LastUpdate { get; set; }

    public string Username { get; set; } = null!;

    public uint? Userid { get; set; }
}
