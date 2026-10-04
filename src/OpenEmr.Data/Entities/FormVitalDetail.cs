using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Detailed information of each vital_forms observation column
/// </summary>
public partial class FormVitalDetail
{
    public long Id { get; set; }

    /// <summary>
    /// FK to vital_forms.id
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// Column name from form_vitals
    /// </summary>
    public string VitalsColumn { get; set; } = null!;

    /// <summary>
    /// FK to list_options.list_id for observation_interpretation
    /// </summary>
    public string? InterpretationListId { get; set; }

    /// <summary>
    /// FK to list_options.option_id for observation_interpretation
    /// </summary>
    public string? InterpretationOptionId { get; set; }

    /// <summary>
    /// Archived original codes value from list_options observation_interpretation
    /// </summary>
    public string? InterpretationCodes { get; set; }

    /// <summary>
    /// Archived original title value from list_options observation_interpretation
    /// </summary>
    public string? InterpretationTitle { get; set; }

    /// <summary>
    /// Medical code explaining reason of the vital observation value in form codesystem:codetype;...;
    /// </summary>
    public string? ReasonCode { get; set; }

    /// <summary>
    /// Human readable text description of the reason_code column
    /// </summary>
    public string? ReasonDescription { get; set; }

    /// <summary>
    /// The status of the reason ie completed, in progress, etc
    /// </summary>
    public string? ReasonStatus { get; set; }
}
