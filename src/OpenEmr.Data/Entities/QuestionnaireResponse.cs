using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class QuestionnaireResponse
{
    public long Id { get; set; }

    public byte[]? Uuid { get; set; }

    /// <summary>
    /// A globally unique id for answer set. String version of UUID
    /// </summary>
    public string? ResponseId { get; set; }

    /// <summary>
    /// questionnaire_repository id for subject questionnaire
    /// </summary>
    public long? QuestionnaireForeignId { get; set; }

    /// <summary>
    /// Id for questionnaire content. String version of UUID
    /// </summary>
    public string? QuestionnaireId { get; set; }

    public string? QuestionnaireName { get; set; }

    public int? PatientId { get; set; }

    /// <summary>
    /// May or may not be associated with an encounter
    /// </summary>
    public int? Encounter { get; set; }

    public int? AuditUserId { get; set; }

    /// <summary>
    /// user id if answers are provider
    /// </summary>
    public int? CreatorUserId { get; set; }

    public DateTime? CreateTime { get; set; }

    public DateTime? LastUpdated { get; set; }

    public int Version { get; set; }

    /// <summary>
    /// form current status. completed,active,incomplete
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// the subject questionnaire json
    /// </summary>
    public string? Questionnaire { get; set; }

    /// <summary>
    /// questionnaire response json
    /// </summary>
    public string? QuestionnaireResponse1 { get; set; }

    /// <summary>
    /// lform answers array json
    /// </summary>
    public string? FormResponse { get; set; }

    /// <summary>
    /// Arithmetic scoring of questionnaires
    /// </summary>
    public int? FormScore { get; set; }

    /// <summary>
    /// T-Score
    /// </summary>
    public double? Tscore { get; set; }

    /// <summary>
    /// Standard error for the T-Score
    /// </summary>
    public double? Error { get; set; }
}
