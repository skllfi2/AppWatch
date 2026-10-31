using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using AppWatch.Catalog;
using AppWatch.Core.Domain.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppWatch.ViewModels;

public sealed partial class BatchImportCandidate : ObservableObject
{
    public required string RawInput { get; init; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    public string RepoOwner { get; init; } = "";
    public string RepoName { get; init; } = "";
    public string? WingetPackageId { get; init; }
    public UpdateKind UpdateKind { get; init; } = UpdateKind.PortableZip;
    public CatalogEntry? CatalogMatch { get; init; }

    public bool IsDuplicate { get; init; }
    public bool IsValid { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public string StatusText { get; init; } = "";

    public string SourceBadge => !string.IsNullOrEmpty(RepoOwner)
        ? $"GitHub: {RepoOwner}/{RepoName}"
        : (!string.IsNullOrEmpty(WingetPackageId) ? $"WinGet: {WingetPackageId}" : "Неизвестно");
}

public sealed partial class BatchImportDialogViewModel : ObservableObject
{
    private static readonly Regex GithubRegex = new(
        @"github\.com[/:]([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ShorthandRegex = new(
        @"^([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+)$",
        RegexOptions.Compiled);

    private readonly IReadOnlyList<TrackedApp> _existingApps;
    private readonly CatalogService _catalog;

    [ObservableProperty]
    public partial string RawText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsAnalyzing { get; set; }

    [ObservableProperty]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    public partial bool CanImport { get; set; }

    [ObservableProperty]
    public partial string AnalysisSummary { get; set; } = "";

    public ObservableCollection<BatchImportCandidate> Candidates { get; } = [];

    public BatchImportDialogViewModel(
        IReadOnlyList<TrackedApp> existingApps,
        CatalogService catalog,
        string? initialText = null)
    {
        _existingApps = existingApps;
        _catalog = catalog;
        if (!string.IsNullOrWhiteSpace(initialText))
        {
            RawText = initialText;
            Analyze();
        }
    }

    [RelayCommand]
    public void Analyze()
    {
        Candidates.Clear();
        var lines = RawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith("//"))
                continue;

            var candidate = ParseLine(line);
            candidate.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(BatchImportCandidate.IsSelected))
                    UpdateCounts();
            };
            Candidates.Add(candidate);
        }

        UpdateCounts();
        AnalysisSummary = $"Распознано: {Candidates.Count(c => c.IsValid)} из {Candidates.Count}";
    }

    public BatchImportCandidate ParseLine(string line)
    {
        string? owner = null;
        string? repo = null;
        string? wingetId = null;
        var isValid = false;

        // 1. Check GitHub URL
        var match = GithubRegex.Match(line);
        if (match.Success)
        {
            owner = match.Groups[1].Value;
            repo = match.Groups[2].Value.Replace(".git", "", StringComparison.OrdinalIgnoreCase);
            isValid = true;
        }
        // 2. Check shorthand owner/repo
        else
        {
            var shortMatch = ShorthandRegex.Match(line);
            if (shortMatch.Success)
            {
                owner = shortMatch.Groups[1].Value;
                repo = shortMatch.Groups[2].Value;
                isValid = true;
            }
            // 3. Check Winget ID (e.g. winget:Microsoft.VisualStudioCode or Publisher.Package)
            else if (line.StartsWith("winget:", StringComparison.OrdinalIgnoreCase))
            {
                wingetId = line["winget:".Length..].Trim();
                isValid = !string.IsNullOrWhiteSpace(wingetId);
            }
            else if (line.Contains('.') && !line.Contains(' ') && !line.Contains('/'))
            {
                // Likely a WinGet package ID like Git.Git or Microsoft.VisualStudioCode
                wingetId = line;
                isValid = true;
            }
        }

        if (!isValid)
        {
            return new BatchImportCandidate
            {
                RawInput = line,
                Name = line,
                IsValid = false,
                IsDuplicate = false,
                IsSelected = false,
                StatusText = "Неверный формат строки"
            };
        }

        // Check catalog match for richer defaults
        CatalogEntry? catalogMatch = null;
        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
        {
            catalogMatch = _catalog.Entries.FirstOrDefault(e =>
                e.RepoOwner.Equals(owner, StringComparison.OrdinalIgnoreCase) &&
                e.RepoName.Equals(repo, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrEmpty(wingetId))
        {
            catalogMatch = _catalog.Entries.FirstOrDefault(e =>
                !string.IsNullOrEmpty(e.WingetPackageId) &&
                e.WingetPackageId.Equals(wingetId, StringComparison.OrdinalIgnoreCase));
        }

        var isDuplicate = false;
        string duplicateReason = "";

        if (!string.IsNullOrEmpty(owner) && !string.IsNullOrEmpty(repo))
        {
            if (_existingApps.Any(a =>
                a.RepoOwner.Equals(owner, StringComparison.OrdinalIgnoreCase) &&
                a.RepoName.Equals(repo, StringComparison.OrdinalIgnoreCase)))
            {
                isDuplicate = true;
                duplicateReason = "Уже отслеживается (GitHub)";
            }
        }
        else if (!string.IsNullOrEmpty(wingetId))
        {
            if (_existingApps.Any(a =>
                !string.IsNullOrEmpty(a.WingetPackageId) &&
                a.WingetPackageId.Equals(wingetId, StringComparison.OrdinalIgnoreCase)))
            {
                isDuplicate = true;
                duplicateReason = "Уже отслеживается (WinGet)";
            }
        }

        var name = catalogMatch?.Name ?? repo ?? wingetId ?? line;
        var updateKind = catalogMatch?.UpdateKind ??
                         (!string.IsNullOrEmpty(wingetId) ? UpdateKind.WingetPackage : UpdateKind.PortableZip);

        return new BatchImportCandidate
        {
            RawInput = line,
            Name = name,
            RepoOwner = owner ?? "",
            RepoName = repo ?? "",
            WingetPackageId = wingetId,
            UpdateKind = updateKind,
            CatalogMatch = catalogMatch,
            IsDuplicate = isDuplicate,
            IsValid = true,
            IsSelected = !isDuplicate,
            StatusText = isDuplicate
                ? duplicateReason
                : (catalogMatch is not null ? "Найдено в каталоге" : "Готово к добавлению")
        };
    }

    [RelayCommand]
    public void SelectAll(bool select)
    {
        foreach (var c in Candidates)
        {
            if (c.IsValid && !c.IsDuplicate)
                c.IsSelected = select;
        }
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        SelectedCount = Candidates.Count(c => c.IsSelected && c.IsValid && !c.IsDuplicate);
        CanImport = SelectedCount > 0;
    }

    public List<TrackedApp> CreateTrackedApps()
    {
        var result = new List<TrackedApp>();

        foreach (var c in Candidates.Where(c => c.IsSelected && c.IsValid && !c.IsDuplicate))
        {
            if (c.CatalogMatch is not null)
            {
                result.Add(c.CatalogMatch.ToTrackedApp());
            }
            else
            {
                result.Add(new TrackedApp
                {
                    Name = c.Name,
                    RepoOwner = c.RepoOwner,
                    RepoName = c.RepoName,
                    WingetPackageId = c.WingetPackageId,
                    UpdateKind = c.UpdateKind,
                    Enabled = true,
                    IgnorePreReleases = true,
                });
            }
        }

        return result;
    }
}
