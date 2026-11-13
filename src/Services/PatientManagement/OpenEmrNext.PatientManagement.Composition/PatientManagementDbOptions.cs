using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Composition;

/// <summary>
/// Connection details for the least-privilege user ADR 0050 provisions (scoped to <c>patient_data</c>
/// and <c>patient_management_outbox</c> only) against OpenEMR's own shared database. Bound from the
/// <c>PatientManagementDb</c> section.
/// </summary>
/// <remarks>
/// Unlike AuthFacade's own database options, there is no fallback for this one: this service's whole
/// job is reading and writing the shared <c>patient_data</c> table (ADR 0049), which doesn't exist
/// anywhere but that real database, so every field here is required, same as <c>OpenEmrOptions</c>.
/// </remarks>
public sealed class PatientManagementDbOptions
{
    /// <summary>The configuration section name, <c>PatientManagementDb</c>.</summary>
    public const string SectionName = "PatientManagementDb";

    /// <summary>The MariaDB server's hostname. Required.</summary>
    [Required]
    public string Host { get; set; } = "";

    /// <summary>The MariaDB server's port.</summary>
    public int Port { get; set; } = 3306;

    /// <summary>OpenEMR's own database name, for example <c>openemr</c>. Required.</summary>
    [Required]
    public string Database { get; set; } = "";

    /// <summary>The least-privilege user scoped to <c>patient_data</c> and <c>patient_management_outbox</c> only. Required.</summary>
    [Required]
    public string User { get; set; } = "";

    /// <summary>The user's password. Required.</summary>
    [Required]
    public string Password { get; set; } = "";
}
