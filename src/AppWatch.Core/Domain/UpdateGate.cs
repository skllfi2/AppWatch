namespace AppWatch.Core.Domain;

/// <summary>
/// UPD-03: per-app update lock. All mutating paths (Apply/Install, card button,
/// toast button, background auto-apply) funnel through the engine — a second
/// concurrent operation on the SAME app is refused cleanly instead of racing
/// on files, processes and the ".old" backup.
/// </summary>
public sealed class UpdateGate
{
    private readonly object _lock = new();
    private readonly HashSet<string> _busy = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Claims the app for an update. Returns a releaser, or null when already updating.</summary>
    public Releaser? TryAcquire(string appKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appKey);
        lock (_lock)
        {
            if (!_busy.Add(appKey)) return null;
            return new Releaser(this, appKey);
        }
    }

    public bool IsBusy(string appKey)
    {
        lock (_lock) return _busy.Contains(appKey);
    }

    private void Release(string appKey)
    {
        lock (_lock) _busy.Remove(appKey);
    }

    /// <summary>Disposes to release the claim; double-dispose is a no-op.</summary>
    public sealed class Releaser : IDisposable
    {
        private UpdateGate? _gate;
        private readonly string _key;

        internal Releaser(UpdateGate gate, string key)
        {
            _gate = gate;
            _key = key;
        }

        public void Dispose()
        {
            _gate?.Release(_key);
            _gate = null;
        }
    }
}
