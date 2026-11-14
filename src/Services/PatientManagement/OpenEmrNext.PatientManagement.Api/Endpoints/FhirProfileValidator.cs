using System.Formats.Tar;
using System.IO.Compression;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Hl7.Fhir.Validation;

namespace OpenEmrNext.PatientManagement.Api.Endpoints;

/// <summary>
/// Validates a FHIR resource against a real profile (US Core Patient, in practice) using Firely's own
/// validator, not a hand-rolled check. Registered as a singleton (DI): building the resolver chain
/// means scanning thousands of StructureDefinition files, and <c>Validator</c>/its resolvers are not
/// thread-safe to construct concurrently but are safe to reuse once built, per Firely's own docs.
/// </summary>
/// <remarks>
/// Resolves profiles from two real FHIR packages (<c>hl7.fhir.r4.core</c>, <c>hl7.fhir.us.core@8.0.0</c>,
/// vendored as the <c>.tgz</c> files NPM-style FHIR packages actually ship as, under
/// <c>FhirProfiles/</c>) extracted into a local cache directory on first use -- not resolved live
/// against a package registry at request time, which would make every validation depend on a third
/// party's uptime, and not through Firely's own newer package-resolution library
/// (<c>Firely.Fhir.Packages</c>), which is built for the 6.x model-package generation and is binary
/// incompatible with the 5.x line this validator needs (confirmed by a real
/// <see cref="TypeLoadException"/>, not assumed) -- see <c>docs/m4-domain-notes.md</c>'s FHIR SDK
/// section for the full story.
/// </remarks>
public sealed class FhirProfileValidator
{
    public const string UsCorePatientProfile = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient";

    private readonly Lazy<Validator> _validator;

    public FhirProfileValidator()
    {
        _validator = new Lazy<Validator>(Build, isThreadSafe: true);
    }

    public OperationOutcome Validate(Resource resource, string profileUrl) =>
        _validator.Value.Validate(resource.ToTypedElement(), profileUrl);

    private static Validator Build()
    {
        var cacheDirectory = Path.Combine(Path.GetTempPath(), "openemr-next-fhir-profiles");
        var profilesDirectory = Path.Combine(AppContext.BaseDirectory, "FhirProfiles");

        var coreDirectory = ExtractIfNeeded(Path.Combine(profilesDirectory, "hl7.fhir.r4.core.tgz"), Path.Combine(cacheDirectory, "core"));
        var usCoreDirectory = ExtractIfNeeded(Path.Combine(profilesDirectory, "hl7.fhir.us.core-8.0.0.tgz"), Path.Combine(cacheDirectory, "uscore"));

        var core = new CommonDirectorySource(ModelInfo.ModelInspector, Path.Combine(coreDirectory, "package"));
        var usCore = new CommonDirectorySource(ModelInfo.ModelInspector, Path.Combine(usCoreDirectory, "package"));
        var resolver = new CachedResolver(new MultiResolver(core, usCore));
        var terminologyService = new LocalTerminologyService(resolver);

        return new Validator(new ValidationSettings
        {
            ResourceResolver = resolver,
            TerminologyService = terminologyService,
            GenerateSnapshot = true
        });
    }

    /// <summary>Idempotent: a marker file next to the extracted <c>package/</c> folder means a
    /// previous run already did this, so a long-running service only pays the extraction cost once
    /// per cache directory, not once per <see cref="Build"/> call.</summary>
    private static string ExtractIfNeeded(string tgzPath, string destinationDirectory)
    {
        var markerFile = Path.Combine(destinationDirectory, ".extracted");
        if (File.Exists(markerFile))
            return destinationDirectory;

        Directory.CreateDirectory(destinationDirectory);
        using (var fileStream = File.OpenRead(tgzPath))
        using (var gzip = new GZipStream(fileStream, CompressionMode.Decompress))
        {
            TarFile.ExtractToDirectory(gzip, destinationDirectory, overwriteFiles: true);
        }
        File.WriteAllText(markerFile, DateTime.UtcNow.ToString("O"));

        return destinationDirectory;
    }
}
