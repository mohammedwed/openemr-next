using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class DrugTemplate
{
    public int DrugId { get; set; }

    public string Selector { get; set; } = null!;

    public string? Dosage { get; set; }

    public int Period { get; set; }

    public int Quantity { get; set; }

    public int Refills { get; set; }

    public string? Taxrates { get; set; }

    /// <summary>
    /// Number of product items per template item
    /// </summary>
    public float Pkgqty { get; set; }
}
