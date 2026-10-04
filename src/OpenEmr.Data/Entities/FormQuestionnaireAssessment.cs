using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormQuestionnaireAssessment
{
    public long Id { get; set; }

    public DateTime? Date { get; set; }

    /// <summary>
    /// The foreign id to the questionnaire_response repository
    /// </summary>
    public string? ResponseId { get; set; }

    public long Pid { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte Authorized { get; set; }

    public sbyte Activity { get; set; }

    public string? Copyright { get; set; }

    public string? FormName { get; set; }

    /// <summary>
    /// json meta data for the response resource
    /// </summary>
    public string? ResponseMeta { get; set; }

    /// <summary>
    /// The foreign id to the questionnaire_repository
    /// </summary>
    public string? QuestionnaireId { get; set; }

    public string? Questionnaire { get; set; }

    public string? QuestionnaireResponse { get; set; }

    public string? Lform { get; set; }

    public string? LformResponse { get; set; }

    public string? Category { get; set; }
}
