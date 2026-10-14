namespace AppWatch.Core.Domain;

/// <summary>One archived snapshot: directory path, version label, archive write time (UTC).</summary>
public sealed record VersionArchiveEntry(string DirectoryPath, string Label, DateTime WrittenUtc);

/// <summary>
/// B10 — pure version-cache policy: which archived snapshots survive pruning.
/// Pure logic over VersionArchiveEntry so it is fully unit-testable;
/// the IO (VersionStore) lives in Infrastructure.
/// </summary>
public static class VersionArchive
{
    /// <summary>
    /// Entries ordered newest-first (write time, then label, descending) —
    /// this is the order the rollback UI shows them in.
    /// </summary>
    public static IReadOnlyList<VersionArchiveEntry> NewestFirst(
        IEnumerable<VersionArchiveEntry> entries) =>
        entries
            .OrderByDescending(e => e.WrittenUtc)
            .ThenByDescending(e => e.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Archive directories to DELETE: everything beyond the newest <paramref name="keep"/>.</summary>
    public static IReadOnlyList<VersionArchiveEntry> PickPruneTargets(
        IReadOnlyList<VersionArchiveEntry> entries, int keep)
    {
        var ordered = NewestFirst(entries);
        return keep >= ordered.Count ? [] : ordered.Skip(Math.Max(0, keep)).ToList();
    }
}