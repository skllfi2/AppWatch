using System.IO.Compression;
using System.Security.Cryptography;

namespace AppWatch.Infrastructure.System;

public sealed class FileStore
{
    public async Task VerifyDigestAsync(string filePath, string expectedSha256)
    {
        await using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
        if (actual != expectedSha256.ToLowerInvariant())
            throw new InvalidDataException(
                $"SHA-256 mismatch: expected {expectedSha256.ToLowerInvariant()}, got {actual}");
    }

    public string ExtractToStaging(string zipPath, string tag, string? targetFileName = null)
    {
        var staging = Path.Combine(Path.GetTempPath(), $"appwatch-extract-{tag}");
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

    public void ReplaceWithRollback(string contentRoot, string installDirectory)
    {
        // P0 live-fix: Directory.Move does not create intermediate directories —
        // %LOCALAPPDATA%\AppWatch\Apps may not exist on first install/update.
        var parent = Path.GetDirectoryName(installDirectory);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        var backup = installDirectory + ".old";
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
        if (Directory.Exists(installDirectory)) Directory.Move(installDirectory, backup);
        try
        {
            Directory.Move(contentRoot, installDirectory);
        }
        catch
        {
            if (Directory.Exists(backup) && !Directory.Exists(installDirectory))
                Directory.Move(backup, installDirectory);
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
        Directory.Move(backup, installDirectory);
    }
}
