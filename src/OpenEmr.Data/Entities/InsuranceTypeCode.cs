using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class InsuranceTypeCode
{
    public int Id { get; set; }

    public string Type { get; set; } = null!;

    public string? ClaimType { get; set; }
}
