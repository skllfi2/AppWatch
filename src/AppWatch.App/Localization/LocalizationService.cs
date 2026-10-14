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
        DumpLoc($"resource names: {string.Join(" | ", assembly.GetManifestResourceNames())}");
        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && n.EndsWith(".json")))
        {
            try
            {
                var language = resourceName.Substring(prefix.Length, resourceName.Length - prefix.Length - ".json".Length);
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null) { DumpLoc($"{resourceName}: stream is null"); continue; }
                using var document = JsonDocument.Parse(stream);
                // Last-wins: a key differing only in case must not nuke the whole
                // language pack (ToDictionary with OrdinalIgnoreCase throws on such dups).
                var pack = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in document.RootElement.EnumerateObject())
                    pack[property.Name] = property.Value.GetString() ?? property.Name;
                packs[language] = pack;
                DumpLoc($"{resourceName}: loaded {packs[language].Count} keys");
            }
            catch (Exception ex)
            {
                DumpLoc($"{resourceName}: FAILED {ex.GetType().Name}: {ex.Message}");
                // Log but continue - don't crash on bad resource
                System.Diagnostics.Debug.WriteLine($"Failed to load localization resource {resourceName}: {ex}");
            }
        }
        // Ensure fallback exists
        if (!packs.ContainsKey(FallbackLanguage))
        {
            packs[FallbackLanguage] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        DumpLoc($"packs: {string.Join(", ", packs.Keys)} (en keys: {packs[FallbackLanguage].Count})");
        return packs;
    }

    private static void DumpLoc(string message)
    {
        try
        {
            var p = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AppWatch", "logs", "loc-debug.log");
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.AppendAllText(p, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
        }
        catch { /* best effort */ }
    }
}
