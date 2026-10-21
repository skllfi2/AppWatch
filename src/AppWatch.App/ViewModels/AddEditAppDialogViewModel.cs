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
    }

    // Expose AvatarUrl for binding to Image in dialog
    public string? AvatarUrl => _app.AvatarUrl;

    public bool IsNewApp => _isNew;

    public string DialogTitle => _isNew ? LocalizationService.Instance["AddAppTitle"] : LocalizationService.Instance["EditAppTitle"];
    public string PrimaryButtonText => _isNew ? LocalizationService.Instance["BtnAdd"] : LocalizationService.Instance["BtnSave"];

    public Loc Loc => Loc.Instance;

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
    }

    partial void OnRepoAvatarUrlChanged(string? value) => OnPropertyChanged(nameof(ShowGitHubInfo));
    partial void OnRepoDescriptionChanged(string? value) => OnPropertyChanged(nameof(ShowGitHubInfo));

    [RelayCommand]
    public async Task FetchRepoInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(RepoOwner) || string.IsNullOrWhiteSpace(RepoName))
        {
            RepoFetchError = Loc.Instance["RepoOwnerNameRequired"];
            return;
        }

        IsFetchingRepoInfo = true;
        RepoFetchError = null;
        RepoDescription = null;
        RepoHomepage = null;
        RepoAvatarUrl = null;
        RepoHtmlUrl = null;

        try
        {
            var info = await _github.GetRepoInfoAsync(RepoOwner.Trim(), RepoName.Trim());
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
                    await AutoDetectFromLatestReleaseAsync(RepoOwner.Trim(), RepoName.Trim());
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