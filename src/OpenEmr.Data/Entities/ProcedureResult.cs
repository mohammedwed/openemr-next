using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProcedureResult
{
    public long ProcedureResultId { get; set; }

    public byte[]? Uuid { get; set; }

    /// <summary>
    /// references procedure_report.procedure_report_id
    /// </summary>
    public long ProcedureReportId { get; set; }

    /// <summary>
    /// N=Numeric, S=String, F=Formatted, E=External, L=Long text as first line of comments
    /// </summary>
    public string ResultDataType { get; set; } = null!;

    /// <summary>
    /// LOINC code, might match a procedure_type.procedure_code
    /// </summary>
    public string ResultCode { get; set; } = null!;

    /// <summary>
    /// Description of result_code
    /// </summary>
    public string ResultText { get; set; } = null!;

    /// <summary>
    /// lab-provided date specific to this result
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// lab-provided testing facility ID
    /// </summary>
    public string Facility { get; set; } = null!;

    public string Units { get; set; } = null!;

    public string Result { get; set; } = null!;

    public string Range { get; set; } = null!;

    /// <summary>
    /// no,yes,high,low
    /// </summary>
    public string Abnormal { get; set; } = null!;

    /// <summary>
    /// comments from the lab
    /// </summary>
    public string? Comments { get; set; }

    /// <summary>
    /// references documents.id if this result is a document
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// preliminary, cannot be done, final, corrected, incomplete...etc.
    /// </summary>
    public string ResultStatus { get; set; } = null!;

    /// <summary>
    /// lab-provided end date specific to this result
    /// </summary>
    public DateTime? DateEnd { get; set; }
}
