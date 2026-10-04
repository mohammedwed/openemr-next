using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProcedureAnswer
{
    /// <summary>
    /// references procedure_order.procedure_order_id
    /// </summary>
    public long ProcedureOrderId { get; set; }

    /// <summary>
    /// references procedure_order_code.procedure_order_seq
    /// </summary>
    public int ProcedureOrderSeq { get; set; }

    /// <summary>
    /// references procedure_questions.question_code
    /// </summary>
    public string QuestionCode { get; set; } = null!;

    /// <summary>
    /// supports multiple-choice questions. answer_seq, incremented in code
    /// </summary>
    public int AnswerSeq { get; set; }

    /// <summary>
    /// answer data
    /// </summary>
    public string Answer { get; set; } = null!;

    public string? ProcedureCode { get; set; }
}
