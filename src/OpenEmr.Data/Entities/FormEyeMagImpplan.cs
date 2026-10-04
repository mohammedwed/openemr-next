using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormEyeMagImpplan
{
    public int Id { get; set; }

    public long FormId { get; set; }

    public long Pid { get; set; }

    public string Title { get; set; } = null!;

    public string? Code { get; set; }

    public string? Codetype { get; set; }

    public string? Codedesc { get; set; }

    public string? Codetext { get; set; }

    public string? Plan { get; set; }

    public string? PmsfhLink { get; set; }

    public sbyte? ImpplanOrder { get; set; }
}
