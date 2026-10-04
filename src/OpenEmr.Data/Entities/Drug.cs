using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Drug
{
    public int DrugId { get; set; }

    public byte[]? Uuid { get; set; }

    public string Name { get; set; } = null!;

    public string NdcNumber { get; set; } = null!;

    public int OnOrder { get; set; }

    public float ReorderPoint { get; set; }

    public float MaxLevel { get; set; }

    public DateOnly? LastNotify { get; set; }

    public string? Reactions { get; set; }

    public string Form { get; set; } = null!;

    public string Size { get; set; } = null!;

    public string Unit { get; set; } = null!;

    public string Route { get; set; } = null!;

    public int Substitute { get; set; }

    /// <summary>
    /// may reference a related codes.code
    /// </summary>
    public string RelatedCode { get; set; } = null!;

    /// <summary>
    /// default units when the related HCPCS code is added to a fee sheet
    /// </summary>
    public int? BillingUnits { get; set; }

    /// <summary>
    /// NDC unit of measure for the related HCPCS service line
    /// </summary>
    public string NdcUom { get; set; } = null!;

    /// <summary>
    /// NDC quantity for the related HCPCS service line
    /// </summary>
    public decimal? NdcQuantity { get; set; }

    /// <summary>
    /// quantity representing a years supply
    /// </summary>
    public float CypFactor { get; set; }

    /// <summary>
    /// 0 = inactive, 1 = active
    /// </summary>
    public bool? Active { get; set; }

    /// <summary>
    /// 1 = allow filling an order from multiple lots
    /// </summary>
    public bool AllowCombining { get; set; }

    /// <summary>
    /// 1 = allow multiple lots at one warehouse
    /// </summary>
    public bool? AllowMultiple { get; set; }

    public string? DrugCode { get; set; }

    /// <summary>
    /// 1 = will not show on the fee sheet
    /// </summary>
    public bool Consumable { get; set; }

    /// <summary>
    /// 0 = pharmacy elsewhere, 1 = dispensed here
    /// </summary>
    public bool? Dispensable { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime LastUpdated { get; set; }
}
