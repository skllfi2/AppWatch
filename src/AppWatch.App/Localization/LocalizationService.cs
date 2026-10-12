using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace AppWatch.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string FallbackLanguage = "en";

    // must initialize BEFORE Instance: the singleton's constructor reads it
    private static readonly Dictionary<string, Dictionary<string, string>> Packs = LoadPacks();

    private Dictionary<string, string> _current =
        Packs.TryGetValue(FallbackLanguage, out var english) ? english : [];

    public static LocalizationService Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => GetString(key);

    public IReadOnlyList<string> AvailableLanguages { get; } = [.. Packs.Keys.OrderBy(x => x)];

    public string GetString(string key) =>
        _current.TryGetValue(key, out var value) ? value
        : Packs.TryGetValue(FallbackLanguage, out var fallback) && fallback.TryGetValue(key, out var fb) ? fb
        : key;

    public string Format(string key, params object[] args) => string.Format(GetString(key), args);

    public void SetLanguage(string language)
    {
        if (!Packs.TryGetValue(language, out var pack)) return;
        _current = pack;
        CultureInfo.CurrentUICulture = new CultureInfo(language);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    private static Dictionary<string, Dictionary<string, string>> LoadPacks()
    {
        var packs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var assembly = Assembly.GetExecutingAssembly();
        const string prefix = "AppWatch.Localization.";
        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".json")))
        {
            var language = resourceName[prefix.Length..^".json".Length];
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) continue;
            using var document = JsonDocument.Parse(stream);
            packs[language] = document.RootElement.EnumerateObject()
                .ToDictionary(
                    p => p.Name,
                    p => p.Value.GetString() ?? p.Name,
                    StringComparer.OrdinalIgnoreCase);
        }
        return packs;
    }
}
