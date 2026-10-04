using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProcedureReport
{
    public long ProcedureReportId { get; set; }

    public byte[]? Uuid { get; set; }

    /// <summary>
    /// references procedure_order.procedure_order_id
    /// </summary>
    public long? ProcedureOrderId { get; set; }

    /// <summary>
    /// references procedure_order_code.procedure_order_seq
    /// </summary>
    public int ProcedureOrderSeq { get; set; }

    public DateTime? DateCollected { get; set; }

    /// <summary>
    /// +-hhmm offset from UTC
    /// </summary>
    public string? DateCollectedTz { get; set; }

    public DateTime? DateReport { get; set; }

    /// <summary>
    /// +-hhmm offset from UTC
    /// </summary>
    public string? DateReportTz { get; set; }

    /// <summary>
    /// references users.id, who entered this data
    /// </summary>
    public long Source { get; set; }

    public string SpecimenNum { get; set; } = null!;

    /// <summary>
    /// received,complete,error
    /// </summary>
    public string ReportStatus { get; set; } = null!;

    /// <summary>
    /// pending review status: received,reviewed
    /// </summary>
    public string ReviewStatus { get; set; } = null!;

    /// <summary>
    /// notes from the lab
    /// </summary>
    public string? ReportNotes { get; set; }
}
