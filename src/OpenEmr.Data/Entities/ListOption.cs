using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ListOption
{
    public string ListId { get; set; } = null!;

    public string OptionId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public int Seq { get; set; }

    public bool IsDefault { get; set; }

    public float OptionValue { get; set; }

    public string Mapping { get; set; } = null!;

    public string? Notes { get; set; }

    public string Codes { get; set; } = null!;

    public bool ToggleSetting1 { get; set; }

    public bool ToggleSetting2 { get; set; }

    public sbyte Activity { get; set; }

    public string Subtype { get; set; } = null!;

    public bool? EditOptions { get; set; }

    public DateTime Timestamp { get; set; }

    public DateTime LastUpdated { get; set; }
}
