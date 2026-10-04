using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProductWarehouse
{
    public int PwDrugId { get; set; }

    public string PwWarehouse { get; set; } = null!;

    public float? PwMinLevel { get; set; }

    public float? PwMaxLevel { get; set; }
}
