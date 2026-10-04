using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class LayoutOption
{
    public string FormId { get; set; } = null!;

    public string FieldId { get; set; } = null!;

    public string GroupId { get; set; } = null!;

    public string? Title { get; set; }

    public int Seq { get; set; }

    public sbyte DataType { get; set; }

    public bool? Uor { get; set; }

    public int FldLength { get; set; }

    public int MaxLength { get; set; }

    public string ListId { get; set; } = null!;

    public sbyte Titlecols { get; set; }

    public sbyte Datacols { get; set; }

    public string DefaultValue { get; set; } = null!;

    public string EditOptions { get; set; } = null!;

    public string? Description { get; set; }

    public int FldRows { get; set; }

    public string ListBackupId { get; set; } = null!;

    /// <summary>
    /// F=Form, D=Demographics, H=History, E=Encounter
    /// </summary>
    public string Source { get; set; } = null!;

    /// <summary>
    /// serialized array of skip conditions
    /// </summary>
    public string? Conditions { get; set; }

    public string? Validation { get; set; }

    public string Codes { get; set; } = null!;
}
