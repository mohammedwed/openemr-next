using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class CodesHistory
{
    public long LogId { get; set; }

    public DateTime? Date { get; set; }

    public string? Code { get; set; }

    public string? Modifier { get; set; }

    public bool? Active { get; set; }

    public bool? DiagnosisReporting { get; set; }

    public bool? FinancialReporting { get; set; }

    public string? Category { get; set; }

    public string? CodeTypeName { get; set; }

    public string? CodeText { get; set; }

    public string? CodeTextShort { get; set; }

    public string? Prices { get; set; }

    public string? ActionType { get; set; }

    public string? UpdateBy { get; set; }
}
