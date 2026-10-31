using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Domain;

public sealed class UpdateSourceRegistry : IUpdateSourceRegistry
{
    private readonly List<IUpdateSourceProvider> _providers = [];
    private readonly object _lock = new();

    public IReadOnlyList<IUpdateSourceProvider> Providers
    {
        get
        {
            lock (_lock) return _providers.ToList();
        }
    }

    public UpdateSourceRegistry(IEnumerable<IUpdateSourceProvider>? initialProviders = null)
    {
        if (initialProviders is not null)
        {
            _providers.AddRange(initialProviders);
        }
    }

    public void Register(IUpdateSourceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_lock)
        {
            if (!_providers.Any(p => p.SourceId.Equals(provider.SourceId, StringComparison.OrdinalIgnoreCase)))
                _providers.Add(provider);
        }
    }

    public IUpdateSourceProvider? Resolve(TrackedApp app)
    {
        ArgumentNullException.ThrowIfNull(app);
        lock (_lock)
        {
            return _providers.FirstOrDefault(p => p.CanHandle(app));
        }
    }
}
