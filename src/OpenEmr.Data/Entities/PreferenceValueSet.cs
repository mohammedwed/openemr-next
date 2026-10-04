using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

/// <summary>
/// Answer lists for preference codes
/// </summary>
public partial class PreferenceValueSet
{
    public int Id { get; set; }

    public string LoincCode { get; set; } = null!;

    public string AnswerCode { get; set; } = null!;

    public string AnswerSystem { get; set; } = null!;

    public string AnswerDisplay { get; set; } = null!;

    public string? AnswerDefinition { get; set; }

    public int? SortOrder { get; set; }

    public bool? Active { get; set; }
}
