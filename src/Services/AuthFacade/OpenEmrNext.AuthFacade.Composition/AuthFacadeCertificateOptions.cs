namespace OpenEmrNext.AuthFacade.Composition;

/// <summary>
/// Paths to the two certificates OpenIddict.Client uses to encrypt and sign its own state tokens (ADR
/// 0045) — persisted files, not the dev-only certificates it otherwise regenerates fresh every process
/// start (the third gap ADR 0028 named). Bound from the <c>AuthFacadeCertificates</c> section.
/// </summary>
/// <remarks>
/// Deliberately optional as a whole, the same shape as <see cref="AuthFacadeDbOptions"/>: when
/// <see cref="EncryptionCertificatePath"/> is unset, AuthFacade falls back to OpenIddict's own
/// per-process development certificates instead — the behavior this project had before this ADR, still
/// used by every unit test and a bare <c>dotnet run</c> outside Docker, neither of which has a persisted
/// certificate file to point at. Only the real Compose stack sets this section.
/// </remarks>
public sealed class AuthFacadeCertificateOptions
{
    /// <summary>The configuration section name, <c>AuthFacadeCertificates</c>.</summary>
    public const string SectionName = "AuthFacadeCertificates";

    /// <summary>Path to the PKCS#12 (.pfx) encryption certificate. Empty means "not configured" — see remarks.</summary>
    public string EncryptionCertificatePath { get; set; } = "";

    /// <summary>Path to the PKCS#12 (.pfx) signing certificate.</summary>
    public string SigningCertificatePath { get; set; } = "";

    /// <summary>The password protecting both PKCS#12 files (the same one for both, generated together).</summary>
    public string Password { get; set; } = "";

    /// <summary>Whether real certificate files are actually configured. False means fall back to OpenIddict's development certificates.</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(EncryptionCertificatePath) && !string.IsNullOrEmpty(SigningCertificatePath);
}
