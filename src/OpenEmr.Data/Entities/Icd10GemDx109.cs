using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Icd10GemDx109
{
    public ulong MapId { get; set; }

    public string? DxIcd10Source { get; set; }

    public string? DxIcd9Target { get; set; }

    public string? Flags { get; set; }

    public sbyte? Active { get; set; }

    public int? Revision { get; set; }
}
