using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Price
{
    public string PrId { get; set; } = null!;

    /// <summary>
    /// template selector for drugs, empty for codes
    /// </summary>
    public string PrSelector { get; set; } = null!;

    public string PrLevel { get; set; } = null!;

    /// <summary>
    /// price in local currency
    /// </summary>
    public decimal PrPrice { get; set; }
}
