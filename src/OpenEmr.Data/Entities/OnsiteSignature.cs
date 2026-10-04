using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class OnsiteSignature
{
    public long Id { get; set; }

    public string Status { get; set; } = null!;

    public string Type { get; set; } = null!;

    public int Created { get; set; }

    public DateTime Lastmod { get; set; }

    public long? Pid { get; set; }

    public int? Encounter { get; set; }

    public string? User { get; set; }

    public sbyte Activity { get; set; }

    public sbyte? Authorized { get; set; }

    public string Signator { get; set; } = null!;

    public string? SigImage { get; set; }

    public string? Signature { get; set; }

    public string SigHash { get; set; } = null!;

    public string Ip { get; set; } = null!;
}
