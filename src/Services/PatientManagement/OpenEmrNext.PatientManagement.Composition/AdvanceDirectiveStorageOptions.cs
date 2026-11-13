using System.ComponentModel.DataAnnotations;

namespace OpenEmrNext.PatientManagement.Composition;

/// <summary>
/// Where advance-directive document bytes get written on local disk. Bound from the
/// <c>AdvanceDirectiveStorage</c> section.
/// </summary>
/// <remarks>
/// Should point at the same path legacy's own <c>$GLOBALS['oer_config']['documents']['repopath']</c>
/// resolves to (<c>sites/default/documents/</c>), via a shared volume, so files this service writes are
/// visible to legacy's own document browser -- this host isn't containerized into the Compose stack
/// yet (docker-compose.yml has no <c>patient-management</c> service), so wiring that shared mount is a
/// deployment task for whenever it is, not something this option can enforce on its own.
/// </remarks>
public sealed class AdvanceDirectiveStorageOptions
{
    /// <summary>The configuration section name, <c>AdvanceDirectiveStorage</c>.</summary>
    public const string SectionName = "AdvanceDirectiveStorage";

    /// <summary>The local directory document files get written under, one subdirectory per patient. Required.</summary>
    [Required]
    public string RepositoryRoot { get; set; } = "";
}
