using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Code
{
    public int Id { get; set; }

    public string? CodeText { get; set; }

    public string? CodeTextShort { get; set; }

    public string Code1 { get; set; } = null!;

    public short? CodeType { get; set; }

    public string Modifier { get; set; } = null!;

    public int? Units { get; set; }

    public decimal? Fee { get; set; }

    public string Superbill { get; set; } = null!;

    public string RelatedCode { get; set; } = null!;

    public string Taxrates { get; set; } = null!;

    /// <summary>
    /// quantity representing a years supply
    /// </summary>
    public float CypFactor { get; set; }

    /// <summary>
    /// 0 = inactive, 1 = active
    /// </summary>
    public bool? Active { get; set; }

    /// <summary>
    /// 0 = non-reportable, 1 = reportable
    /// </summary>
    public bool? Reportable { get; set; }

    /// <summary>
    /// 0 = negative, 1 = considered important code in financial reporting
    /// </summary>
    public bool? FinancialReporting { get; set; }

    /// <summary>
    /// Item revenue code
    /// </summary>
    public string RevenueCode { get; set; } = null!;
}
