using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Catalog;

/// <summary>
/// Curated app catalog shipped inside the app (no developer backend needed).
/// The catalog is updated together with the app itself via self-update.
/// </summary>
public sealed class CatalogService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public IReadOnlyList<CatalogEntry> Entries { get; } = LoadEntries();

    private static List<CatalogEntry> LoadEntries()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("AppWatch.Catalog.catalog.json");
        if (stream is null) return [];
        return JsonSerializer.Deserialize<CatalogFile>(stream, Options)?.Entries ?? [];
    }

    private sealed class CatalogFile
    {
        public List<CatalogEntry> Entries { get; set; } = [];
    }
}
