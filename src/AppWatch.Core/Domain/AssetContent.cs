namespace AppWatch.Core.Domain;

/// <summary>
/// Classifies a release asset by content: archive (needs extraction) vs a single
/// self-contained executable (copy as-is into the install directory).
/// Real-world driver: itsfatduck/optimizerDuck ships bare per-arch .exe files
/// (~87 MB, no archive wrapper) — the "просто exe" case from the product brief.
/// </summary>
public static class AssetContent
{
    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".tar.gz"];
    private static readonly string[] SingleFileExtensions = [".exe", ".msi"];

    public static bool IsArchive(string assetName)
    {
        var name = assetName.ToLowerInvariant();
        return ArchiveExtensions.Any(name.EndsWith);
    }

    public static bool IsSingleExecutable(string assetName)
    {
        var name = assetName.ToLowerInvariant();
        return SingleFileExtensions.Any(name.EndsWith);
    }
}
