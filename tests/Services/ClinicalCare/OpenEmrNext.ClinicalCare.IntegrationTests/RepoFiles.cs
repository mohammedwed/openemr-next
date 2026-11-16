namespace OpenEmrNext.ClinicalCare.IntegrationTests;

/// <summary>Finds files in the repository from wherever the tests run.</summary>
internal static class RepoFiles
{
    /// <summary>Walks up from the test binary to the repository root, so tests run from any working directory.</summary>
    public static string Find(string relativePath)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Could not find '{relativePath}' above {AppContext.BaseDirectory}.");
    }
}
