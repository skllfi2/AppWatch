namespace AppWatch.Core.Domain;

/// <summary>
/// UPD-02: crash-recovery journal for an in-flight update. Written right before
/// the mutating phase, cleared after commit. A leftover record at startup means
/// AppWatch died mid-update (kill / power loss / crash).
/// </summary>
public sealed record PendingUpdate(
    string AppName,
    string InstallDirectory,
    string? FromVersion,
    string ToVersion,
    DateTimeOffset StartedUtc)
{
    public const string FileName = "pending_update.json";
}

/// <summary>Pure decision for a leftover journal record. Kept separate from file IO for unit tests.</summary>
public static class PendingUpdatePolicy
{
    public enum Action
    {
        /// <summary>Nothing was mutated (crash before the replace phase) — just forget it.</summary>
        ClearOnly,
        /// <summary>Files were replaced but never health-checked — restore the ".old" backup.</summary>
        RestoreBackup,
    }

    /// <summary>
    /// backupExists = the install dir's ".old" sibling (created by ReplaceWithRollback) is present.
    /// Idempotent by construction: restoring moves ".old" back, so a repeated recovery finds no backup.
    /// </summary>
    public static Action Decide(bool backupExists)
        => backupExists ? Action.RestoreBackup : Action.ClearOnly;
}
