using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProcedureOrderCode
{
    /// <summary>
    /// references procedure_order.procedure_order_id
    /// </summary>
    public long ProcedureOrderId { get; set; }

    /// <summary>
    /// Supports multiple tests per order. Procedure_order_seq, incremented in code
    /// </summary>
    public int ProcedureOrderSeq { get; set; }

    /// <summary>
    /// like procedure_type.procedure_code
    /// </summary>
    public string ProcedureCode { get; set; } = null!;

    /// <summary>
    /// descriptive name of the procedure code
    /// </summary>
    public string ProcedureName { get; set; } = null!;

    /// <summary>
    /// 1=original order, 2=added after order sent
    /// </summary>
    public string ProcedureSource { get; set; } = null!;

    /// <summary>
    /// diagnoses and maybe other coding (e.g. ICD9:111.11)
    /// </summary>
    public string? Diagnoses { get; set; }

    /// <summary>
    /// 0 = normal, 1 = do not transmit to lab
    /// </summary>
    public bool DoNotSend { get; set; }

    public string? ProcedureOrderTitle { get; set; }

    public string? ProcedureType { get; set; }

    public string? Transport { get; set; }

    public DateTime? DateEnd { get; set; }

    public string? ReasonCode { get; set; }

    public string? ReasonDescription { get; set; }

    public DateTime? ReasonDateLow { get; set; }

    public DateTime? ReasonDateHigh { get; set; }

    public string? ReasonStatus { get; set; }
}
