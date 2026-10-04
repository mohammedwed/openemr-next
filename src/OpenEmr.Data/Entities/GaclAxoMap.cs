using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class GaclAxoMap
{
    public int AclId { get; set; }

    public string SectionValue { get; set; } = null!;

    public string Value { get; set; } = null!;
}
