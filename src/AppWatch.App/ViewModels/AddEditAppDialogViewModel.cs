using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AppWatch.Catalog;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.Infrastructure.System;
using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppWatch.ViewModels;

public sealed class AssetChoiceItem
{
    public string Name { get; }
    public long Size { get; }
    public string DisplayText { get; }
    public ReleaseAsset Asset { get; }

    public AssetChoiceItem(ReleaseAsset asset)
    {
        Asset = asset;
        Name = asset.Name;
        Size = asset.Size;
        var sizeMb = asset.Size > 0 ? $" ({asset.Size / (1024.0 * 1024.0):F1} МБ)" : "";
        DisplayText = $"{asset.Name}{sizeMb}";
    }

    public override string ToString() => DisplayText;
}

public sealed partial class AddEditAppDialogViewModel : ObservableObject
{
    private readonly TrackedApp _app;
    private readonly bool _isNew;
    private readonly IGitHubClient _github;
    private readonly WingetService _winget;
    private readonly CatalogService _catalog;
    private readonly AppConfig _config;
    private readonly IShortcutService _shortcuts;

    public AddEditAppDialogViewModel(
        TrackedApp? app = null,
        IGitHubClient? github = null,
        WingetService? winget = null,
        CatalogService? catalog = null,
        AppConfig? config = null,
        IShortcutService? shortcuts = null)
    {
        _app = app ?? new TrackedApp
        {
            Name = "",
            RepoOwner = "",
            RepoName = "",
            UpdateKind = UpdateKind.PortableZip,
            ExecutableName = "",
            ProcessName = "",
            InstallDirectory = "",
            LaunchAfterUpdate = true,
            IgnorePreReleases = true,
            AutoDetectVersion = true,
            Strategy = UpdateStrategy.Auto,
        };
        _isNew = app is null;
        _github = github ?? App.Resolve<IGitHubClient>();
        _winget = winget ?? App.Resolve<WingetService>();
        _catalog = catalog ?? App.Resolve<CatalogService>();
        _config = config ?? App.Resolve<AppConfig>();
        _shortcuts = shortcuts ?? App.Resolve<IShortcutService>();

        // Initialize from app
        Name = _app.Name;
        RepoOwner = _app.RepoOwner;
        RepoName = _app.RepoName;
        SelectedUpdateKindIndex = (int)_app.UpdateKind;
        SelectedUpdateStrategyIndex = (int)_app.Strategy;
        ExecutableName = _app.ExecutableName ?? "";
        ProcessName = _app.ProcessName ?? "";
        InstallDirectory = _app.InstallDirectory ?? "";
        WingetPackageId = _app.WingetPackageId ?? "";
        VersionFilePath = _app.VersionFilePath ?? "";
        VersionFileRegex = _app.VersionFileRegex ?? "";
        LaunchAfterUpdate = _app.LaunchAfterUpdate;
        IgnorePreReleases = _app.IgnorePreReleases;
        AllowUnsignedInstaller = _app.AllowUnsignedInstaller;
        AutoDetectVersion = _app.AutoDetectVersion;
        RepoInput = !string.IsNullOrWhiteSpace(_app.RepoOwner) && !string.IsNullOrWhiteSpace(_app.RepoName)
            ? $"{_app.RepoOwner}/{_app.RepoName}" : "";
        Description = _app.Description ?? "";
        RepoDescription = _app.Description;

        CheckDuplicate();
    }

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    // Expose AvatarUrl for binding to Image in dialog
    public string? AvatarUrl => _app.AvatarUrl;

    public bool IsNewApp => _isNew;

    public string DialogTitle => _isNew ? LocalizationService.Instance["AddAppTitle"] : LocalizationService.Instance["EditAppTitle"];
    public string PrimaryButtonText => _isNew ? LocalizationService.Instance["BtnAdd"] : LocalizationService.Instance["BtnSave"];

    public Loc Loc => Loc.Instance;

    /// <summary>Single repo input: accepts "owner/repo", "github.com/owner/repo", full https URL, or "owner repo".</summary>
    [ObservableProperty]
    public partial string RepoInput { get; set; } = "";

    /// <summary>Parsed from RepoInput — shown after fetch: "PortableZip · rg.exe".</summary>
    [ObservableProperty]
    public partial string? DetectedText { get; set; }

    [ObservableProperty]
    public partial string? LocalFileStatus { get; set; }

    [ObservableProperty]
    public partial string? DuplicateWarning { get; set; }

    public bool HasDuplicateWarning => !string.IsNullOrWhiteSpace(DuplicateWarning);

    [ObservableProperty]
    public partial bool InstallImmediately { get; set; }

    [ObservableProperty]
    public partial bool CreateStartShortcut { get; set; }

    [ObservableProperty]
    public partial bool CreateDesktopShortcut { get; set; }

    // WinGet search state
    [ObservableProperty]
    public partial bool IsSearchingWinget { get; set; }

    [ObservableProperty]
    public partial string? WingetSearchStatus { get; set; }

    [ObservableProperty]
    public partial bool HasWingetSearchResults { get; set; }

    public ObservableCollection<WingetSearchResult> WingetSearchResults { get; } = [];

    // Available Release Assets state
    public ObservableCollection<AssetChoiceItem> AvailableAssets { get; } = [];

    [ObservableProperty]
    public partial int SelectedAssetIndex { get; set; } = -1;

    [ObservableProperty]
    public partial bool HasAvailableAssets { get; set; }

    /// <summary>Primary button gate: name + (owner & repo) for GitHub kinds; name + winget id for winget.</summary>
    public bool CanAdd
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return false;
            if (SelectedUpdateKindIndex == (int)UpdateKind.WingetPackage)
                return !string.IsNullOrWhiteSpace(WingetPackageId);
            return !string.IsNullOrWhiteSpace(RepoOwner) && !string.IsNullOrWhiteSpace(RepoName);
        }
    }

    public static (string? Owner, string? Repo) ParseRepoInput(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return (null, null);
        var s = input.Trim();

        // URL: https://github.com/owner/repo(.git)?/?
        var m = System.Text.RegularExpressions.Regex.Match(s,
            @"(?:https?://)?(?:www\.)?github\.com/([A-Za-z0-9_.\-]+)/([A-Za-z0-9_.\-]+?)(?:\.git)?/?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success) return (m.Groups[1].Value, m.Groups[2].Value);

        // owner/repo
        var parts = s.Split('/', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2) return (parts[0], parts[1]);

        // "owner repo" — copy-paste tolerance
        parts = s.Split(' ', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2) return (parts[0], parts[1]);

        return (null, null);
    }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string RepoOwner { get; set; } = "";

    [ObservableProperty]
    public partial string RepoName { get; set; } = "";

    [ObservableProperty]
    public partial int SelectedUpdateKindIndex { get; set; } = 0;

    [ObservableProperty]
    public partial int SelectedUpdateStrategyIndex { get; set; } = 0;

    [ObservableProperty]
    public partial string ExecutableName { get; set; } = "";

    [ObservableProperty]
    public partial string ProcessName { get; set; } = "";

    [ObservableProperty]
    public partial string InstallDirectory { get; set; } = "";

    [ObservableProperty]
    public partial string WingetPackageId { get; set; } = "";

    /// <summary>P3: data file containing the version string (env-expanded path).</summary>
    [ObservableProperty]
    public partial string VersionFilePath { get; set; } = "";

    /// <summary>P3: regex with one capture group extracting the version from the file.</summary>
    [ObservableProperty]
    public partial string VersionFileRegex { get; set; } = "";

    [ObservableProperty]
    public partial bool LaunchAfterUpdate { get; set; } = true;

    /// <summary>B7: per-app consent to run unsigned installers.</summary>
    [ObservableProperty]
    public partial bool AllowUnsignedInstaller { get; set; }

    [ObservableProperty]
    public partial bool IgnorePreReleases { get; set; } = true;

    [ObservableProperty]
    public partial bool AutoDetectVersion { get; set; } = true;

    // GitHub repo info (fetched on demand)
    [ObservableProperty]
    public partial string? RepoDescription { get; set; }

    [ObservableProperty]
    public partial string? RepoHomepage { get; set; }

    [ObservableProperty]
    public partial string? RepoAvatarUrl { get; set; }

    [ObservableProperty]
    public partial string? RepoHtmlUrl { get; set; }

    [ObservableProperty]
    public partial bool IsFetchingRepoInfo { get; set; }

    [ObservableProperty]
    public partial string? RepoFetchError { get; set; }

    public bool ShowUpdateStrategy => SelectedUpdateKindIndex != (int)UpdateKind.WingetPackage;

    public bool ShowExecutableName => SelectedUpdateKindIndex != (int)UpdateKind.WingetPackage;

    public bool ShowProcessName => SelectedUpdateKindIndex != (int)UpdateKind.WingetPackage;

    public bool ShowWingetPackageId => SelectedUpdateKindIndex == (int)UpdateKind.WingetPackage;

    public bool ShowGitHubInfo => !string.IsNullOrWhiteSpace(RepoAvatarUrl) || !string.IsNullOrWhiteSpace(RepoDescription);

    partial void OnSelectedUpdateKindIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowUpdateStrategy));
        OnPropertyChanged(nameof(ShowExecutableName));
        OnPropertyChanged(nameof(ShowProcessName));
        OnPropertyChanged(nameof(ShowWingetPackageId));
        OnPropertyChanged(nameof(CanAdd));
        CheckDuplicate();
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanAdd));
        CheckDuplicate();
    }

    partial void OnRepoOwnerChanged(string value)
    {
        OnPropertyChanged(nameof(CanAdd));
        CheckDuplicate();
    }

    partial void OnRepoNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanAdd));
        CheckDuplicate();
    }

    partial void OnWingetPackageIdChanged(string value)
    {
        OnPropertyChanged(nameof(CanAdd));
        CheckDuplicate();
    }

    partial void OnRepoAvatarUrlChanged(string? value) => OnPropertyChanged(nameof(ShowGitHubInfo));
    partial void OnRepoDescriptionChanged(string? value) => OnPropertyChanged(nameof(ShowGitHubInfo));

    partial void OnSelectedAssetIndexChanged(int value)
    {
        if (value >= 0 && value < AvailableAssets.Count)
        {
            var chosen = AvailableAssets[value].Asset;
            var ext = System.IO.Path.GetExtension(chosen.Name).ToLowerInvariant();
            if (ext == ".zip" || ext == ".7z")
            {
                SelectedUpdateKindIndex = (int)UpdateKind.PortableZip;
            }
            else if (ext is ".msi" or ".exe" && (chosen.Name.Contains("setup", StringComparison.OrdinalIgnoreCase) || chosen.Name.Contains("installer", StringComparison.OrdinalIgnoreCase)))
            {
                SelectedUpdateKindIndex = (int)UpdateKind.Installer;
            }
            else if (ext == ".exe")
            {
                SelectedUpdateKindIndex = (int)UpdateKind.PortableExe;
                ExecutableName = chosen.Name;
            }
        }
    }

    public void CheckDuplicate()
    {
        if (_config is null || _config.TrackedApps is null) return;
        TrackedApp? dup = null;

        if (SelectedUpdateKindIndex == (int)UpdateKind.WingetPackage)
        {
            if (!string.IsNullOrWhiteSpace(WingetPackageId))
            {
                dup = _config.TrackedApps.FirstOrDefault(a =>
                    !ReferenceEquals(a, _app) &&
                    !string.IsNullOrWhiteSpace(a.WingetPackageId) &&
                    a.WingetPackageId.Equals(WingetPackageId.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(RepoOwner) && !string.IsNullOrWhiteSpace(RepoName))
            {
                dup = _config.TrackedApps.FirstOrDefault(a =>
                    !ReferenceEquals(a, _app) &&
                    a.RepoOwner.Equals(RepoOwner.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    a.RepoName.Equals(RepoName.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }

        DuplicateWarning = dup is not null
            ? string.Format(LocalizationService.Instance["AppAlreadyTracked"], dup.Name)
            : null;
        OnPropertyChanged(nameof(HasDuplicateWarning));
    }

    [RelayCommand]
    public async Task FetchRepoInfoAsync()
    {
        var (owner, repo) = ParseRepoInput(RepoInput);
        if (owner is null || repo is null)
        {
            RepoFetchError = Loc.Instance["RepoInputInvalid"];
            return;
        }
        RepoOwner = owner;
        RepoName = repo;

        IsFetchingRepoInfo = true;
        RepoFetchError = null;
        RepoDescription = null;
        RepoHomepage = null;
        RepoAvatarUrl = null;
        RepoHtmlUrl = null;
        DetectedText = null;

        try
        {
            var info = await _github.GetRepoInfoAsync(owner, repo);
            if (info is not null)
            {
                RepoDescription = info.Description;
                if (string.IsNullOrWhiteSpace(Description) && !string.IsNullOrWhiteSpace(info.Description))
                    Description = info.Description;
                RepoHomepage = info.Homepage;
                RepoAvatarUrl = info.IconUrl ?? info.AvatarUrl ?? info.OwnerAvatarUrl;
                RepoHtmlUrl = info.HtmlUrl;

                // Auto-fill app name if empty
                if (string.IsNullOrWhiteSpace(Name))
                    Name = info.Name;

                // Auto-detect UpdateKind and ExecutableName from latest release
                if (_isNew) // only for new apps, don't override user's existing choices
                {
                    await AutoDetectFromLatestReleaseAsync(owner, repo);
                }
            }
            else
            {
                RepoFetchError = Loc.Instance["RepoNotFound"];
            }
        }
        catch (Exception ex)
        {
            RepoFetchError = $"{Loc.Instance["RepoFetchFailed"]}: {ex.Message}";
        }
        finally
        {
            IsFetchingRepoInfo = false;
            CheckDuplicate();
        }
    }

    private async Task AutoDetectFromLatestReleaseAsync(string owner, string repo)
    {
        try
        {
            var releases = await _github.GetReleasesAsync(owner, repo);
            var filtered = IgnorePreReleases
                ? releases.Where(r => !r.IsPreRelease).ToList()
                : releases;

            if (filtered.Count > 0)
            {
                var latest = filtered[0];
                var assetNames = latest.Assets.Select(a => a.Name).ToList();

                // Populate available release assets
                AvailableAssets.Clear();
                var winAssets = latest.Assets.Where(a =>
                {
                    var n = a.Name.ToLowerInvariant();
                    return n.EndsWith(".zip") || n.EndsWith(".7z") || n.EndsWith(".exe") || n.EndsWith(".msi");
                }).ToList();

                if (winAssets.Count == 0)
                    winAssets = latest.Assets.ToList();

                foreach (var a in winAssets)
                    AvailableAssets.Add(new AssetChoiceItem(a));

                HasAvailableAssets = AvailableAssets.Count > 0;

                var suggestedKind = StarsImportHeuristics.SuggestUpdateKind(assetNames);
                if (suggestedKind.HasValue)
                {
                    SelectedUpdateKindIndex = (int)suggestedKind.Value;
                    var suggestedExe = StarsImportHeuristics.SuggestExecutableName(assetNames, suggestedKind.Value);
                    if (!string.IsNullOrWhiteSpace(suggestedExe))
                        ExecutableName = suggestedExe;

                    if (suggestedKind.Value is UpdateKind.PortableZip or UpdateKind.PortableExe)
                    {
                        if (string.IsNullOrWhiteSpace(InstallDirectory))
                        {
                            var cfg = App.Resolve<AppConfig>();
                            var root = cfg.InstallRoot ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppWatch", "Apps");
                            InstallDirectory = System.IO.Path.Combine(root, repo);
                        }
                    }
                    else if (suggestedKind.Value is UpdateKind.Installer)
                    {
                        InstallDirectory = "";
                    }

                    // Pre-select best asset in dropdown
                    var bestIdx = 0;
                    if (!string.IsNullOrWhiteSpace(suggestedExe))
                    {
                        for (int i = 0; i < AvailableAssets.Count; i++)
                        {
                            if (AvailableAssets[i].Name.Contains(suggestedExe, StringComparison.OrdinalIgnoreCase))
                            {
                                bestIdx = i;
                                break;
                            }
                        }
                    }
                    if (AvailableAssets.Count > 0)
                        SelectedAssetIndex = bestIdx;

                    // Show the user what was detected
                    var kindLabel = suggestedKind.Value switch
                    {
                        UpdateKind.PortableZip => Loc.Instance["UpdateKindPortableZip"],
                        UpdateKind.PortableExe => Loc.Instance["UpdateKindPortableExe"],
                        UpdateKind.Installer => Loc.Instance["UpdateKindInstaller"],
                        UpdateKind.WingetPackage => Loc.Instance["UpdateKindWingetPackage"],
                        UpdateKind.External => Loc.Instance["UpdateKindExternal"],
                        _ => suggestedKind.Value.ToString(),
                    };
                    DetectedText = string.Format(LocalizationService.Instance["DetectedLabel"],
                        kindLabel, suggestedExe ?? "—");
                }
            }
        }
        catch
        {
            // best-effort; ignore auto-detect failures
        }
    }

    [RelayCommand]
    public void OpenRepoInBrowser()
    {
        if (!string.IsNullOrWhiteSpace(RepoHtmlUrl))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = RepoHtmlUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }
    }

    [RelayCommand]
    public async Task SearchWingetPackageAsync(CancellationToken cancellationToken = default)
    {
        var q = !string.IsNullOrWhiteSpace(WingetPackageId)
            ? WingetPackageId.Trim()
            : (!string.IsNullOrWhiteSpace(Name) ? Name.Trim() : "");

        if (string.IsNullOrWhiteSpace(q))
        {
            WingetSearchStatus = LocalizationService.Instance["WingetSearchPlaceholder"];
            return;
        }

        IsSearchingWinget = true;
        WingetSearchStatus = LocalizationService.Instance["WingetSearching"];
        WingetSearchResults.Clear();
        HasWingetSearchResults = false;

        try
        {
            var results = await _winget.SearchAsync(q, cancellationToken);
            foreach (var r in results.Take(15))
                WingetSearchResults.Add(r);

            HasWingetSearchResults = WingetSearchResults.Count > 0;
            WingetSearchStatus = results.Count == 0
                ? LocalizationService.Instance["WingetNoResults"]
                : null;
        }
        catch (Exception ex)
        {
            WingetSearchStatus = ex.Message;
        }
        finally
        {
            IsSearchingWinget = false;
        }
    }

    public void SelectWingetPackage(WingetSearchResult pkg)
    {
        WingetPackageId = pkg.Id;
        if (string.IsNullOrWhiteSpace(Name) || Name == pkg.Id)
            Name = pkg.Name;
        WingetSearchResults.Clear();
        HasWingetSearchResults = false;
        WingetSearchStatus = null;
        CheckDuplicate();
    }

    public async Task PickLocalFileAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        await ApplyLocalFileAsync(file.Path);
    }

    public async Task ApplyLocalFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        var targetPath = filePath;
        if (targetPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = _shortcuts.ResolveShortcutTarget(targetPath);
            if (!string.IsNullOrEmpty(resolved))
                targetPath = resolved;
        }

        if (!System.IO.File.Exists(targetPath)) return;

        var dir = System.IO.Path.GetDirectoryName(targetPath) ?? "";
        var fileName = System.IO.Path.GetFileName(targetPath);
        var baseName = System.IO.Path.GetFileNameWithoutExtension(targetPath);

        System.Diagnostics.FileVersionInfo? vi = null;
        try { vi = System.Diagnostics.FileVersionInfo.GetVersionInfo(targetPath); } catch { }

        var prodName = !string.IsNullOrWhiteSpace(vi?.ProductName) ? vi.ProductName : baseName;
        var version = vi?.ProductVersion ?? vi?.FileVersion ?? "";

        InstallDirectory = dir;
        ExecutableName = fileName;
        ProcessName = baseName;
        if (string.IsNullOrWhiteSpace(Name))
            Name = prodName;

        LocalFileStatus = string.Format(LocalizationService.Instance["LocalFileDetected"], prodName, version);

        // Try fuzzy matching with Catalog
        if (string.IsNullOrWhiteSpace(RepoInput) && _catalog is not null)
        {
            var matches = CatalogSearch.Rank(_catalog.Entries, prodName);
            if (matches.Count > 0)
            {
                var entry = matches[0];
                RepoInput = $"{entry.RepoOwner}/{entry.RepoName}";
                await FetchRepoInfoAsync();
            }
        }

        CheckDuplicate();
    }

    public IReadOnlyList<string> GetRunningProcesses()
    {
        try
        {
            return System.Diagnostics.Process.GetProcesses()
                .Where(p => !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                .Select(p => p.ProcessName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public TrackedApp ToTrackedApp()
    {
        _app.Name = Name.Trim();
        _app.RepoOwner = RepoOwner.Trim();
        _app.RepoName = RepoName.Trim();
        _app.UpdateKind = (UpdateKind)SelectedUpdateKindIndex;
        _app.Strategy = (UpdateStrategy)SelectedUpdateStrategyIndex;
        _app.ExecutableName = string.IsNullOrWhiteSpace(ExecutableName) ? null : ExecutableName.Trim();
        _app.ProcessName = string.IsNullOrWhiteSpace(ProcessName) ? null : ProcessName.Trim();
        _app.InstallDirectory = string.IsNullOrWhiteSpace(InstallDirectory) ? null : InstallDirectory.Trim();
        _app.WingetPackageId = string.IsNullOrWhiteSpace(WingetPackageId) ? null : WingetPackageId.Trim();
        _app.VersionFilePath = string.IsNullOrWhiteSpace(VersionFilePath) ? null : VersionFilePath.Trim();
        _app.VersionFileRegex = string.IsNullOrWhiteSpace(VersionFileRegex) ? null : VersionFileRegex.Trim();
        _app.LaunchAfterUpdate = LaunchAfterUpdate;
        _app.IgnorePreReleases = IgnorePreReleases;
        _app.AllowUnsignedInstaller = AllowUnsignedInstaller;
        _app.AutoDetectVersion = AutoDetectVersion;
        _app.AvatarUrl = RepoAvatarUrl; // persist avatar URL
        _app.Description = string.IsNullOrWhiteSpace(Description)
            ? (string.IsNullOrWhiteSpace(RepoDescription) ? null : RepoDescription.Trim())
            : Description.Trim();
        return _app;
    }
}
