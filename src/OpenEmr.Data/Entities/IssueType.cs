using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class IssueType
{
    public bool? Active { get; set; }

    public string Category { get; set; } = null!;

    public string Type { get; set; } = null!;

    public string Plural { get; set; } = null!;

    public string Singular { get; set; } = null!;

    public string Abbreviation { get; set; } = null!;

    public short Style { get; set; }

    public short ForceShow { get; set; }

    public int Ordering { get; set; }

    public string AcoSpec { get; set; } = null!;
}
