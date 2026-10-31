using System.Collections.ObjectModel;
using AppWatch.Catalog;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppWatch.ViewModels;

public sealed partial class SystemScanCandidate : ObservableObject
{
    public required string DisplayName { get; init; }
    public string? DisplayVersion { get; init; }
    public string? InstallLocation { get; init; }
    public string? ExecutableName { get; set; }
    public string? DisplayIcon { get; init; }
    public string RegistryKeyName { get; init; } = "";

    public CatalogEntry? CatalogMatch { get; init; }
    public bool IsCatalogMatch => CatalogMatch is not null;
    public bool IsAlreadyTracked { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string SourceLabel => CatalogMatch is not null
        ? $"Каталог: {CatalogMatch.RepoOwner}/{CatalogMatch.RepoName}"
        : "Локально установлено";

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(DisplayVersion)) parts.Add($"v{DisplayVersion}");
            if (!string.IsNullOrWhiteSpace(InstallLocation)) parts.Add(InstallLocation);
            return parts.Count > 0 ? string.Join(" • ", parts) : "";
        }
    }

    public string Monogram
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) return "?";
            var parts = DisplayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][0].ToString().ToUpperInvariant();
            return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpperInvariant();
        }
    }
}

public sealed partial class SystemScannerDialogViewModel : ObservableObject
{
    private readonly SystemVersionResolver _systemVersion;
    private readonly CatalogService _catalog;
    private readonly IReadOnlyList<TrackedApp> _existingApps;
    private readonly List<SystemScanCandidate> _allCandidates = [];

    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    [ObservableProperty]
    public partial string SearchFilter { get; set; } = "";

    [ObservableProperty]
    public partial bool OnlyCatalogMatches { get; set; } = false;

    [ObservableProperty]
    public partial string ScanStatusText { get; set; } = "";

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    public partial bool CanAdd { get; set; }

    public ObservableCollection<SystemScanCandidate> FilteredCandidates { get; } = [];

    public SystemScannerDialogViewModel(
        SystemVersionResolver systemVersion,
        CatalogService catalog,
        IReadOnlyList<TrackedApp> existingApps)
    {
        _systemVersion = systemVersion;
        _catalog = catalog;
        _existingApps = existingApps;
    }

    partial void OnSearchFilterChanged(string value) => ApplyFilter();
    partial void OnOnlyCatalogMatchesChanged(bool value) => ApplyFilter();

    [RelayCommand]
    public async Task ScanAsync()
    {
        IsScanning = true;
        ScanStatusText = "Сканирование установленных программ...";
        _allCandidates.Clear();
        FilteredCandidates.Clear();

        await Task.Run(() =>
        {
            var installed = _systemVersion.GetAllInstalledApps();
            var catalogEntries = _catalog.Entries;

            foreach (var rec in installed)
            {
                if (string.IsNullOrWhiteSpace(rec.DisplayName)) continue;

                // Skip Windows system updates and runtime redistributables unless catalog specifically matches
                if (IsSystemNoise(rec.DisplayName)) continue;

                // 1. Check duplicate against existing tracked apps
                var isAlreadyTracked = _existingApps.Any(a =>
                    (!string.IsNullOrWhiteSpace(a.RegistryKeyName) &&
                     a.RegistryKeyName.Equals(rec.RegistryKeyName, StringComparison.OrdinalIgnoreCase)) ||
                    a.Name.Equals(rec.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(a.InstallDirectory) &&
                     !string.IsNullOrWhiteSpace(rec.InstallLocation) &&
                     Path.GetFullPath(a.InstallDirectory).Equals(Path.GetFullPath(rec.InstallLocation), StringComparison.OrdinalIgnoreCase)));

                // 2. Check catalog match
                CatalogEntry? matchedCatalog = null;

                // Exact match by name
                matchedCatalog = catalogEntries.FirstOrDefault(e =>
                    e.Name.Equals(rec.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                    e.RepoName.Equals(rec.DisplayName, StringComparison.OrdinalIgnoreCase));

                // Fuzzy match via CatalogSearch.Rank if no exact match
                if (matchedCatalog is null)
                {
                    var ranked = CatalogSearch.Rank(catalogEntries, rec.DisplayName);
                    if (ranked.Count > 0)
                    {
                        var top = ranked[0];
                        if (rec.DisplayName.Contains(top.Name, StringComparison.OrdinalIgnoreCase) ||
                            top.Name.Contains(rec.DisplayName, StringComparison.OrdinalIgnoreCase))
                        {
                            matchedCatalog = top;
                        }
                    }
                }

                // If already tracked by repo
                if (matchedCatalog is not null && !isAlreadyTracked)
                {
                    if (_existingApps.Any(a =>
                        a.RepoOwner.Equals(matchedCatalog.RepoOwner, StringComparison.OrdinalIgnoreCase) &&
                        a.RepoName.Equals(matchedCatalog.RepoName, StringComparison.OrdinalIgnoreCase)))
                    {
                        isAlreadyTracked = true;
                    }
                }

                // Resolve executable
                string? exe = null;
                if (!string.IsNullOrWhiteSpace(rec.InstallLocation) && Directory.Exists(rec.InstallLocation))
                {
                    exe = SystemVersionResolver.FindPrimaryExecutable(
                        rec.InstallLocation,
                        matchedCatalog?.ExecutableName ?? matchedCatalog?.Name ?? rec.DisplayName);
                }

                var candidate = new SystemScanCandidate
                {
                    DisplayName = rec.DisplayName,
                    DisplayVersion = rec.DisplayVersion,
                    InstallLocation = rec.InstallLocation,
                    ExecutableName = exe ?? matchedCatalog?.ExecutableName,
                    DisplayIcon = rec.DisplayIcon,
                    RegistryKeyName = rec.RegistryKeyName,
                    CatalogMatch = matchedCatalog,
                    IsAlreadyTracked = isAlreadyTracked,
                    IsSelected = !isAlreadyTracked && (matchedCatalog is not null),
                };

                candidate.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SystemScanCandidate.IsSelected))
                        UpdateSelectedCount();
                };

                _allCandidates.Add(candidate);
            }

            // Sort: Catalog matches first, then un-tracked, then already tracked
            _allCandidates.Sort((a, b) =>
            {
                if (a.IsAlreadyTracked != b.IsAlreadyTracked)
                    return a.IsAlreadyTracked ? 1 : -1;
                if (a.IsCatalogMatch != b.IsCatalogMatch)
                    return a.IsCatalogMatch ? -1 : 1;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
        });

        ApplyFilter();
        IsScanning = false;
        ScanStatusText = $"Найдено программ: {_allCandidates.Count} (из каталога: {_allCandidates.Count(c => c.IsCatalogMatch)})";
    }

    public void ApplyFilter()
    {
        FilteredCandidates.Clear();
        var q = SearchFilter.Trim();

        foreach (var c in _allCandidates)
        {
            if (OnlyCatalogMatches && !c.IsCatalogMatch) continue;
            if (!string.IsNullOrEmpty(q) &&
                !c.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                !(c.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            FilteredCandidates.Add(c);
        }

        UpdateSelectedCount();
    }

    [RelayCommand]
    public void SelectAll(bool select)
    {
        foreach (var c in FilteredCandidates)
        {
            if (!c.IsAlreadyTracked)
                c.IsSelected = select;
        }
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = _allCandidates.Count(c => c.IsSelected && !c.IsAlreadyTracked);
        CanAdd = SelectedCount > 0;
    }

    public List<TrackedApp> CreateTrackedApps()
    {
        var result = new List<TrackedApp>();

        foreach (var candidate in _allCandidates.Where(c => c.IsSelected && !c.IsAlreadyTracked))
        {
            if (candidate.CatalogMatch is not null)
            {
                var app = candidate.CatalogMatch.ToTrackedApp(
                    installDirectory: candidate.InstallLocation,
                    currentVersion: candidate.DisplayVersion);

                if (!string.IsNullOrWhiteSpace(candidate.ExecutableName))
                    app.ExecutableName = candidate.ExecutableName;
                if (!string.IsNullOrWhiteSpace(candidate.RegistryKeyName))
                    app.RegistryKeyName = candidate.RegistryKeyName;

                result.Add(app);
            }
            else
            {
                var app = new TrackedApp
                {
                    Name = candidate.DisplayName,
                    RepoOwner = "",
                    RepoName = "",
                    UpdateKind = UpdateKind.Installer,
                    InstallDirectory = candidate.InstallLocation,
                    ExecutableName = candidate.ExecutableName,
                    CurrentVersion = candidate.DisplayVersion,
                    RegistryKeyName = candidate.RegistryKeyName,
                    Enabled = true,
                    IgnorePreReleases = true,
                };
                result.Add(app);
            }
        }

        return result;
    }

    private static bool IsSystemNoise(string name)
    {
        var n = name.ToLowerInvariant();
        return n.StartsWith("update for ") ||
               n.StartsWith("security update") ||
               n.StartsWith("hotfix for") ||
               n.Contains("windows driver package") ||
               n.Contains("microsoft visual c++") ||
               n.Contains(".net framework") ||
               n.Contains(".net core") ||
               n.Contains("windows sdk") ||
               n.Contains("directx");
    }
}
