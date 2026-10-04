using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Icd9DxLongCode
{
    public ulong DxId { get; set; }

    public string? DxCode { get; set; }

    public string? LongDesc { get; set; }

    public sbyte? Active { get; set; }

    public int? Revision { get; set; }
}
