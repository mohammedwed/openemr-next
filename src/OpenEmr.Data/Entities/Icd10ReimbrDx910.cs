using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Icd10ReimbrDx910
{
    public ulong MapId { get; set; }

    public string? Code { get; set; }

    public sbyte? CodeCnt { get; set; }

    public string? Icd901 { get; set; }

    public string? Icd902 { get; set; }

    public string? Icd903 { get; set; }

    public string? Icd904 { get; set; }

    public string? Icd905 { get; set; }

    public string? Icd906 { get; set; }

    public sbyte? Active { get; set; }

    public int? Revision { get; set; }
}
