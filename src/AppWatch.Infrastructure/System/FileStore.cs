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
        // renamed to the app's ExecutableName when configured.
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
    /// Safely replaces an executable or binary file even if it is currently locked by a running process.
    /// In Windows, locked executables allow rename (Move) to a temporary name within the same volume,
    /// after which the new binary can be placed immediately at the original path.
    /// </summary>
    public static bool SafeReplaceFile(string sourceFilePath, string destinationFilePath)
    {
        ArgumentNullException.ThrowIfNull(sourceFilePath);
        ArgumentNullException.ThrowIfNull(destinationFilePath);

        var destDir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        // Try direct overwrite first
        try
        {
            File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            // Destination file is likely locked by a running process
        }
        catch (UnauthorizedAccessException)
        {
            // Or access denied due to executable lock
        }

        // Hot-Patching strategy:
        // 1. Rename running locked file to destinationFilePath.old_PID_GUID
        // 2. Copy new binary into place
        // 3. Mark old file for deletion on restart or cleanup
        var tempOldPath = destinationFilePath + ".old_" + Guid.NewGuid().ToString("N");
        try
        {
            File.Move(destinationFilePath, tempOldPath, overwrite: true);
        }
        catch
        {
            return false;
        }

        try
        {
            File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
        }
        catch
        {
            // Rollback rename if copy fails
            try { File.Move(tempOldPath, destinationFilePath, overwrite: true); } catch { }
            return false;
        }

        // Try immediate cleanup of the old file (succeeds once the process exits)
        try { File.Delete(tempOldPath); } catch { }
        return true;
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
            SafeReplaceFile(file, targetFile);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var targetSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, targetSubDir);
        }
    }

    public void ReplaceWithRollback(string contentRoot, string installDirectory)
    {
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
