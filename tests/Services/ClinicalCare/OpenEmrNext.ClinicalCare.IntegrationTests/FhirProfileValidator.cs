using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification.Terminology;
using Hl7.Fhir.Validation;

namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>
/// Validates a FHIR resource against a US Core profile (ADR 0079 #9), for the tests only: nothing in the running service validates its own output. It is built the way Patient Management's validator is (ADR 0052) and
/// reads the <b>same</b> two vendored package files, in place, so the 15 MB of profiles exist once in the repository rather than twice.
/// </summary>
internal sealed class FhirProfileValidator
{
    public const string UsCoreImmunization = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-immunization";

    private static readonly Lazy<Validator> Shared = new(Build, isThreadSafe: true);

    public static OperationOutcome Validate(Resource resource, string profileUrl) => Shared.Value.Validate(resource.ToTypedElement(), profileUrl);

    private static Validator Build()
    {
        var profiles = Path.GetDirectoryName(RepoFiles.Find("src/Services/PatientManagement/OpenEmrNext.PatientManagement.Api/FhirProfiles/hl7.fhir.r4.core.tgz"))!;
        var cache = Path.Combine(Path.GetTempPath(), "openemr-next-fhir-profiles-cc-tests");

        var core = new CommonDirectorySource(ModelInfo.ModelInspector, Path.Combine(Extract(Path.Combine(profiles, "hl7.fhir.r4.core.tgz"), Path.Combine(cache, "core")), "package"));
        var usCore = new CommonDirectorySource(ModelInfo.ModelInspector, Path.Combine(Extract(Path.Combine(profiles, "hl7.fhir.us.core-8.0.0.tgz"), Path.Combine(cache, "uscore")), "package"));
        var resolver = new CachedResolver(new MultiResolver(core, usCore));

        return new Validator(new ValidationSettings { ResourceResolver = resolver, TerminologyService = new LocalTerminologyService(resolver), GenerateSnapshot = true });
    }

    private static string Extract(string tgz, string destination)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(tgz)));
        var directory = Path.Combine(destination, hash);
        var marker = Path.Combine(directory, ".extracted");
        if (File.Exists(marker) && File.ReadAllText(marker) == hash)
            return directory;

        Directory.CreateDirectory(directory);
        using (var file = File.OpenRead(tgz))
        using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            TarFile.ExtractToDirectory(gzip, directory, overwriteFiles: true);

        File.WriteAllText(marker, hash);
        return directory;
    }
}
