using System.ComponentModel;
using System.Runtime.CompilerServices;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppWatch.ViewModels;

public sealed partial class AddEditAppDialogViewModel : ObservableObject
{
    private readonly TrackedApp _app;
    private readonly bool _isNew;
    private readonly IGitHubClient _github;

    public AddEditAppDialogViewModel(TrackedApp? app = null, IGitHubClient? github = null)
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
    }

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
    }

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(CanAdd));
    partial void OnRepoOwnerChanged(string value) => OnPropertyChanged(nameof(CanAdd));
    partial void OnRepoNameChanged(string value) => OnPropertyChanged(nameof(CanAdd));
    partial void OnWingetPackageIdChanged(string value) => OnPropertyChanged(nameof(CanAdd));

    partial void OnRepoAvatarUrlChanged(string? value) => OnPropertyChanged(nameof(ShowGitHubInfo));
    partial void OnRepoDescriptionChanged(string? value) => OnPropertyChanged(nameof(ShowGitHubInfo));

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
                RepoHomepage = info.Homepage;
                RepoAvatarUrl = info.OwnerAvatarUrl;
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

                var suggestedKind = StarsImportHeuristics.SuggestUpdateKind(assetNames);
                if (suggestedKind.HasValue)
                {
                    SelectedUpdateKindIndex = (int)suggestedKind.Value;
                    var suggestedExe = StarsImportHeuristics.SuggestExecutableName(assetNames, suggestedKind.Value);
                    if (!string.IsNullOrWhiteSpace(suggestedExe))
                        ExecutableName = suggestedExe;

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
        return _app;
    }
}