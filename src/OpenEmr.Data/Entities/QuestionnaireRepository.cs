using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class QuestionnaireRepository
{
    public ulong Id { get; set; }

    public byte[]? Uuid { get; set; }

    public string? QuestionnaireId { get; set; }

    public uint? Provider { get; set; }

    public int Version { get; set; }

    public DateTime? CreatedDate { get; set; }

    public DateTime? ModifiedDate { get; set; }

    public string? Name { get; set; }

    public string Type { get; set; } = null!;

    public string? Profile { get; set; }

    public bool? Active { get; set; }

    public string? Status { get; set; }

    public string? SourceUrl { get; set; }

    public string? Code { get; set; }

    public string? CodeDisplay { get; set; }

    public string? Questionnaire { get; set; }

    public string? Lform { get; set; }

    /// <summary>
    /// Used for grouping and organizing 
    /// </summary>
    public string? Category { get; set; }
}
