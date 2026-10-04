using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class Billing
{
    public int Id { get; set; }

    public DateTime? Date { get; set; }

    public string? CodeType { get; set; }

    public string? Code { get; set; }

    public long? Pid { get; set; }

    public int? ProviderId { get; set; }

    public int? User { get; set; }

    public string? Groupname { get; set; }

    public bool? Authorized { get; set; }

    public int? Encounter { get; set; }

    public string? CodeText { get; set; }

    public bool? Billed { get; set; }

    public bool? Activity { get; set; }

    public int? PayerId { get; set; }

    public sbyte BillProcess { get; set; }

    public DateTime? BillDate { get; set; }

    public DateTime? ProcessDate { get; set; }

    public string? ProcessFile { get; set; }

    public string? Modifier { get; set; }

    public int? Units { get; set; }

    public decimal? Fee { get; set; }

    public string? Justify { get; set; }

    public string? Target { get; set; }

    public int? X12PartnerId { get; set; }

    public string? NdcInfo { get; set; }

    public string Notecodes { get; set; } = null!;

    public string? ExternalId { get; set; }

    public string? Pricelevel { get; set; }

    /// <summary>
    /// Item revenue code
    /// </summary>
    public string RevenueCode { get; set; } = null!;

    /// <summary>
    /// Charge category or customer
    /// </summary>
    public string? Chargecat { get; set; }
}
