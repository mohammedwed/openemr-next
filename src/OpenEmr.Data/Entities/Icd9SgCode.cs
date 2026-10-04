using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Icd9SgCode
{
    public ulong SgId { get; set; }

    public string? SgCode { get; set; }

    public string? FormattedSgCode { get; set; }

    public string? ShortDesc { get; set; }

    public string? LongDesc { get; set; }

    public sbyte? Active { get; set; }

    public int? Revision { get; set; }
}
