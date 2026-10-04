using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class GaclAcoMap
{
    public int AclId { get; set; }

    public string SectionValue { get; set; } = null!;

    public string Value { get; set; } = null!;
}
