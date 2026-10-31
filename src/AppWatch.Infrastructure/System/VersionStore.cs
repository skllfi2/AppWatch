using AppWatch.Core.Domain;

namespace AppWatch.Infrastructure.System;

/// <summary>
/// B10 — version cache in %LOCALAPPDATA%\AppWatch\Versions\<app>\<version>\.
/// On a successful update the engine archives the old install dir here instead of
/// deleting it; the UI offers instant rollback to any archived snapshot.
/// Prune policy is the pure VersionArchive (Core) — this class is IO only.
/// Supports cross-volume rollback and backups cleanly.
/// </summary>
public sealed class VersionStore
{
    private readonly string _root;
    private readonly int _keepCount;

    public VersionStore(int keepCount = 3, string? rootDirectory = null)
    {
        _keepCount = Math.Max(1, keepCount);
        _root = rootDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppWatch", "Versions");
    }

    /// <summary>Move the post-update backup (installDir + ".old") into the archive.</summary>
    public void ArchiveBackup(string installDirectory, string appName, string? versionLabel)
    {
        var backup = installDirectory + ".old";
        if (!Directory.Exists(backup)) return;

        var appDir = Path.Combine(_root, Sanitize(appName));
        Directory.CreateDirectory(appDir);

        var label = Sanitize(string.IsNullOrWhiteSpace(versionLabel) ? $"unknown-{DateTime.Now:yyyyMMdd-HHmmss}" : versionLabel);
        var target = Path.Combine(appDir, label);
        if (Directory.Exists(target)) Directory.Delete(target, true);

        FileStore.MoveDirectory(backup, target);

        // D5: keep the RAW version next to the sanitized label — "0560" → "v0.5.6.0"
        if (!string.IsNullOrWhiteSpace(versionLabel))
        {
            try { File.WriteAllText(Path.Combine(target, MetadataFileName), versionLabel); }
            catch (IOException) { /* metadata is best-effort */ }
        }

        Prune(appName);
    }

    private const string MetadataFileName = ".appwatch-version";

    /// <summary>Archived snapshots for an app, newest first (rollback UI order).</summary>
    public IReadOnlyList<VersionArchiveEntry> List(string appName)
    {
        var appDir = Path.Combine(_root, Sanitize(appName));
        if (!Directory.Exists(appDir)) return [];

        var entries = Directory.GetDirectories(appDir)
            .Select(dir => new VersionArchiveEntry(
                dir,
                Path.GetFileName(dir),
                Directory.GetLastWriteTimeUtc(dir),
                ReadMetadata(dir)))
            .ToList();
        return VersionArchive.NewestFirst(entries);
    }

    private static string? ReadMetadata(string snapshotDir)
    {
        try
        {
            var path = Path.Combine(snapshotDir, MetadataFileName);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (IOException) { return null; }
    }

    /// <summary>True when at least one archived snapshot exists for the app.</summary>
    public bool Any(string appName) => List(appName).Count > 0;

    /// <summary>
    /// Instant rollback: archive the CURRENT version (so it can be rolled back to),
    /// then move the chosen snapshot into the install directory. Caller must stop the
    /// app process first. The chosen snapshot is consumed (removed from the archive).
    /// Works across different filesystem volumes.
    /// </summary>
    public void RollbackTo(string installDirectory, string appName, VersionArchiveEntry target)
    {
        if (!Directory.Exists(target.DirectoryPath))
            throw new DirectoryNotFoundException($"archived version '{target.Label}' is missing");

        var appDir = Path.Combine(_root, Sanitize(appName));
        Directory.CreateDirectory(appDir);

        // current → archive (roll-forward insurance), then snapshot → install dir
        if (Directory.Exists(installDirectory))
        {
            var currentLabel = $"current-{DateTime.Now:yyyyMMdd-HHmmss}";
            var currentTarget = Path.Combine(appDir, Sanitize(currentLabel));
            FileStore.MoveDirectory(installDirectory, currentTarget);
        }

        FileStore.MoveDirectory(target.DirectoryPath, installDirectory);

        // D5: don't leave the archive metadata dotfile in the live install dir
        try { File.Delete(Path.Combine(installDirectory, MetadataFileName)); }
        catch (IOException) { /* cosmetic */ }

        Prune(appName);
    }

    private void Prune(string appName)
    {
        var all = List(appName);
        foreach (var prune in VersionArchive.PickPruneTargets(all, _keepCount))
        {
            try { Directory.Delete(prune.DirectoryPath, true); }
            catch (IOException) { /* in-use file — keep it, prune next round */ }
        }
    }

    /// <summary>Total size in bytes of all cached version snapshots.</summary>
    public long GetTotalSizeBytes()
    {
        if (!Directory.Exists(_root)) return 0;
        try
        {
            var di = new DirectoryInfo(_root);
            return di.EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return f.Length; }
                    catch { return 0L; }
                });
        }
        catch { return 0; }
    }

    /// <summary>Deletes all cached version snapshots across all apps.</summary>
    public void ClearAll()
    {
        if (!Directory.Exists(_root)) return;
        try
        {
            foreach (var dir in Directory.GetDirectories(_root))
            {
                try { Directory.Delete(dir, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            foreach (var file in Directory.GetFiles(_root))
            {
                try { File.Delete(file); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch { }
    }

    /// <summary>
    /// Sanitizes values for directory names while preserving valid filename characters
    /// (including dots, dashes, underscores, and spaces) so version numbers like "1.2.3"
    /// and app names like "Node.js" remain recognizable and unique.
    /// </summary>
    public static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var sanitized = new string(chars).Trim().Trim('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized[..Math.Min(sanitized.Length, 64)];
    }
}
