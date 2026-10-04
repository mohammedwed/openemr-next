using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class GaclAxoSection
{
    public int Id { get; set; }

    public string Value { get; set; } = null!;

    public int OrderValue { get; set; }

    public string Name { get; set; } = null!;

    public int Hidden { get; set; }
}
