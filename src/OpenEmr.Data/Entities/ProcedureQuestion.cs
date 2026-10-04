using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProcedureQuestion
{
    /// <summary>
    /// references procedure_providers.ppid to identify the lab
    /// </summary>
    public long LabId { get; set; }

    /// <summary>
    /// references procedure_type.procedure_code to identify this order type
    /// </summary>
    public string ProcedureCode { get; set; } = null!;

    /// <summary>
    /// code identifying this question
    /// </summary>
    public string QuestionCode { get; set; } = null!;

    /// <summary>
    /// sequence number for ordering
    /// </summary>
    public int Seq { get; set; }

    /// <summary>
    /// descriptive text for question_code
    /// </summary>
    public string QuestionText { get; set; } = null!;

    /// <summary>
    /// 1 = required, 0 = not
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// maximum length if text input field
    /// </summary>
    public int Maxsize { get; set; }

    /// <summary>
    /// Text, Number, Select, Multiselect, Date, Gestational-age
    /// </summary>
    public string Fldtype { get; set; } = null!;

    /// <summary>
    /// choices for fldtype S and T
    /// </summary>
    public string? Options { get; set; }

    /// <summary>
    /// Additional instructions for answering the question
    /// </summary>
    public string Tips { get; set; } = null!;

    /// <summary>
    /// 1 = active, 0 = inactive
    /// </summary>
    public bool? Activity { get; set; }
}
