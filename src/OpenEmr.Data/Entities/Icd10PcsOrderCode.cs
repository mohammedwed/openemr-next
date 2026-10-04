using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Icd10PcsOrderCode
{
    public ulong PcsId { get; set; }

    public string? PcsCode { get; set; }

    public string? ValidForCoding { get; set; }

    public string? ShortDesc { get; set; }

    public string? LongDesc { get; set; }

    public sbyte? Active { get; set; }

    public int? Revision { get; set; }
}
