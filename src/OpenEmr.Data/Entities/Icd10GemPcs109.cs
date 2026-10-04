using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Icd10GemPcs109
{
    public ulong MapId { get; set; }

    public string? PcsIcd10Source { get; set; }

    public string? PcsIcd9Target { get; set; }

    public string? Flags { get; set; }

    public sbyte? Active { get; set; }

    public int? Revision { get; set; }
}
