using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.ClinicalCare.Composition;

/// <summary>
/// Connection details for the least-privilege user the <c>seed-clinical-care-db</c> job provisions
/// (ADR 0064): scoped to the Clinical Care tables it writes (<c>form_encounter</c>, <c>forms</c>, its own
/// outbox, ...) and nothing else, never OpenEMR's broader app user. Bound from the
/// <c>ClinicalCareDb</c> section.
/// </summary>
/// <remarks>
/// No fallback values: this service's whole job is reading and writing legacy tables that exist only in
/// OpenEMR's real database, so every field is required and a missing one fails startup (OPS-3).
/// </remarks>
public sealed class ClinicalCareDbOptions
{
    /// <summary>The configuration section name, <c>ClinicalCareDb</c>.</summary>
    public const string SectionName = "ClinicalCareDb";

    /// <summary>The MariaDB server's hostname. Required.</summary>
    [Required]
    public string Host { get; set; } = "";

    /// <summary>The MariaDB server's port.</summary>
    public int Port { get; set; } = 3306;

    /// <summary>OpenEMR's own database name, for example <c>openemr</c>. Required.</summary>
    [Required]
    public string Database { get; set; } = "";

    /// <summary>The least-privilege user for this service. Required.</summary>
    [Required]
    public string User { get; set; } = "";

    /// <summary>The user's password. Required.</summary>
    [Required]
    public string Password { get; set; } = "";
}
