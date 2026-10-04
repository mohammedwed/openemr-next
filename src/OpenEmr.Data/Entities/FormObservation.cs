using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class FormObservation
{
    public long Id { get; set; }

    /// <summary>
    /// UUID for the observation, used as unique logical identifier
    /// </summary>
    public byte[]? Uuid { get; set; }

    /// <summary>
    /// FK to forms.form_id
    /// </summary>
    public long FormId { get; set; }

    public DateTime? Date { get; set; }

    public long? Pid { get; set; }

    public string? Encounter { get; set; }

    public string? User { get; set; }

    public string? Groupname { get; set; }

    public sbyte? Authorized { get; set; }

    public sbyte? Activity { get; set; }

    public string? Code { get; set; }

    public string? Observation { get; set; }

    public string? ObValue { get; set; }

    public string? ObUnit { get; set; }

    public string? Description { get; set; }

    public string? CodeType { get; set; }

    public string? TableCode { get; set; }

    public string? ObCode { get; set; }

    public string? ObType { get; set; }

    public string? ObStatus { get; set; }

    public string? ResultStatus { get; set; }

    public string? ObReasonStatus { get; set; }

    public string? ObReasonCode { get; set; }

    public string? ObReasonText { get; set; }

    public string? ObDocumentationofTable { get; set; }

    public long? ObDocumentationofTableId { get; set; }

    public DateTime? DateEnd { get; set; }

    /// <summary>
    /// FK to parent observation for sub-observations
    /// </summary>
    public long? ParentObservationId { get; set; }

    /// <summary>
    /// FK to list_options.option_id for observation category (SDOH, Functional, Cognitive, Physical, etc)
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// FK to questionnaire_response table
    /// </summary>
    public long? QuestionnaireResponseId { get; set; }

    public string? ObValueCodeDescription { get; set; }
}
