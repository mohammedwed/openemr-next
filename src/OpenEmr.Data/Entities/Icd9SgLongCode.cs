using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Icd9SgLongCode
{
    public ulong SqId { get; set; }

    public string? SgCode { get; set; }

    public string? LongDesc { get; set; }

    public sbyte? Active { get; set; }

    public int? Revision { get; set; }
}
