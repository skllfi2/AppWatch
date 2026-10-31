using System.IO.Compression;
using System.Security.Cryptography;

namespace AppWatch.Infrastructure.System;

public sealed class FileStore
{
    public async Task VerifyDigestAsync(string filePath, string expectedSha256)
    {
        await using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream).ConfigureAwait(false));
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"SHA-256 mismatch: expected {expectedSha256.ToLowerInvariant()}, got {actual.ToLowerInvariant()}");
    }

    public string ExtractToStaging(string zipPath, string tag, string? targetFileName = null)
    {
        var safeTag = VersionStore.Sanitize(tag);
        var staging = Path.Combine(Path.GetTempPath(), $"appwatch-extract-{safeTag}");
        if (Directory.Exists(staging)) Directory.Delete(staging, true);

        // P0 follow-up ("просто exe"): single-file releases (optimizerDuck bare .exe)
        // are staged as-is — a copy of the file inside an otherwise empty staging dir,
        // so ReplaceWithRollback keeps working uniformly on directories. The file is
        // renamed to the app's ExecutableName when configured (release asset names carry
        // arch/version suffixes the card's Launch button doesn't know about).
        if (!AppWatch.Core.Domain.AssetContent.IsArchive(zipPath))
        {
            Directory.CreateDirectory(staging);
            File.Copy(zipPath, Path.Combine(staging, targetFileName ?? Path.GetFileName(zipPath)));
            return staging;
        }

        ZipFile.ExtractToDirectory(zipPath, staging);

        // release zips often wrap content in a single root folder — treat it as the content root
        var entries = Directory.GetFileSystemEntries(staging);
        return entries.Length == 1 && Directory.Exists(entries[0]) ? entries[0] : staging;
    }

    /// <summary>
    /// Moves a directory, seamlessly supporting moves across different filesystem volumes/drives.
    /// Fast atomic Directory.Move on same drive; recursive copy + delete on cross-volume moves.
    /// </summary>
    public static void MoveDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        var destParent = Path.GetDirectoryName(destDir);
        if (!string.IsNullOrEmpty(destParent))
            Directory.CreateDirectory(destParent);

        var sourceRoot = Path.GetPathRoot(Path.GetFullPath(sourceDir));
        var destRoot = Path.GetPathRoot(Path.GetFullPath(destDir));

        // Fast atomic rename when on the exact same volume
        if (string.Equals(sourceRoot, destRoot, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Directory.Move(sourceDir, destDir);
                return;
            }
            catch (IOException)
            {
                // Fall back to copy + delete if Directory.Move encounters a volume or junction restriction
            }
        }

        // Cross-volume fallback
        CopyDirectoryRecursive(sourceDir, destDir);
        Directory.Delete(sourceDir, recursive: true);
    }

    public static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var targetFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, targetFile, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, targetSubDir);
        }
    }

    public void ReplaceWithRollback(string contentRoot, string installDirectory)
    {
        // P0 live-fix: Directory.Move does not create intermediate directories —
        // %LOCALAPPDATA%\AppWatch\Apps may not exist on first install/update.
        var parent = Path.GetDirectoryName(installDirectory);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        var backup = installDirectory + ".old";
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
        if (Directory.Exists(installDirectory)) MoveDirectory(installDirectory, backup);
        try
        {
            MoveDirectory(contentRoot, installDirectory);
        }
        catch
        {
            if (Directory.Exists(backup) && !Directory.Exists(installDirectory))
                MoveDirectory(backup, installDirectory);
            throw;
        }
    }

    public void DiscardBackup(string installDirectory)
    {
        var backup = installDirectory + ".old";
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
    }

    public void RestoreBackup(string installDirectory)
    {
        var backup = installDirectory + ".old";
        if (!Directory.Exists(backup)) return;
        if (Directory.Exists(installDirectory)) Directory.Delete(installDirectory, true);
        MoveDirectory(backup, installDirectory);
    }
}
