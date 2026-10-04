using System;
using System.Collections.Generic;

namespace OpenEmr.Data.Entities;

public partial class ProductRegistration
{
    public int Id { get; set; }

    public string? Email { get; set; }

    public bool? OptOut { get; set; }

    public int? AuthById { get; set; }

    /// <summary>
    /// 1 opted out, disabled. NULL ask. 0 use option scopes
    /// </summary>
    public bool? TelemetryDisabled { get; set; }

    public DateTime? LastAskDate { get; set; }

    public string? LastAskVersion { get; set; }

    /// <summary>
    /// JSON array of scope options
    /// </summary>
    public string? Options { get; set; }
}
