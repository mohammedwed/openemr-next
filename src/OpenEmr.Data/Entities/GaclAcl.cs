using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class GaclAcl
{
    public int Id { get; set; }

    public string SectionValue { get; set; } = null!;

    public int Allow { get; set; }

    public int Enabled { get; set; }

    public string? ReturnValue { get; set; }

    public string? Note { get; set; }

    public int UpdatedDate { get; set; }
}
