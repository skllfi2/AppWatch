using AppWatch.Core.Domain;

namespace AppWatch.Infrastructure.System;

/// <summary>
/// B10 — version cache in %LOCALAPPDATA%\AppWatch\Versions\<app>\<version>\.
/// On a successful update the engine archives the old install dir here instead of
/// deleting it; the UI offers instant rollback to any archived snapshot.
/// Prune policy is the pure VersionArchive (Core) — this class is IO only.
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

        Directory.Move(backup, target);
        Prune(appName);
    }

    /// <summary>Archived snapshots for an app, newest first (rollback UI order).</summary>
    public IReadOnlyList<VersionArchiveEntry> List(string appName)
    {
        var appDir = Path.Combine(_root, Sanitize(appName));
        if (!Directory.Exists(appDir)) return [];

        var entries = Directory.GetDirectories(appDir)
            .Select(dir => new VersionArchiveEntry(
                dir,
                Path.GetFileName(dir),
                Directory.GetLastWriteTimeUtc(dir)))
            .ToList();
        return VersionArchive.NewestFirst(entries);
    }

    /// <summary>True when at least one archived snapshot exists for the app.</summary>
    public bool Any(string appName) => List(appName).Count > 0;

    /// <summary>
    /// Instant rollback: archive the CURRENT version (so it can be rolled back to),
    /// then move the chosen snapshot into the install directory. Caller must stop the
    /// app process first. The chosen snapshot is consumed (removed from the archive).
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
            Directory.Move(installDirectory, currentTarget);
        }

        Directory.Move(target.DirectoryPath, installDirectory);
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

    private static string Sanitize(string value) =>
        string.Concat(value.Where(char.IsLetterOrDigit).Take(48));
}