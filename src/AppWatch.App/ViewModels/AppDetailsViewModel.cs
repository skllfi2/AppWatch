using System.Collections.ObjectModel;
using System.Diagnostics;
using AppWatch.Core.Abstractions;
using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.System;
using AppWatch.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AppWatch.ViewModels;

/// <summary>
/// MVVM ViewModel for the per-app details and control hub dialog.
/// Provides rich metadata (links, authors, source kind), action commands
/// (launch, update, rollback, pin, folder), and version history/snapshots.
/// </summary>
public partial class AppDetailsViewModel : ObservableObject
{
    private readonly TrackedAppItem _item;
    private readonly Action<bool> _onConsentChanged;
    private readonly IAppLauncher? _launcher;
    private readonly VersionStore? _versions;
    private readonly IGitHubClient? _github;

    public TrackedAppItem Item => _item;
    public TrackedApp App => _item.App;

    public string Name => _item.Name;
    public string? Description => _item.App.Description;
    public string Version => _item.Version;
    public string? AvailableVersion => _item.AvailableVersion;
    public bool UpdateAvailable => _item.UpdateAvailable;
    public bool RollbackAvailable => _item.RollbackAvailable;
    public BitmapImage? Icon => _item.Icon;
    public string Monogram => _item.Monogram;
    public Microsoft.UI.Xaml.Media.Brush MonogramBrush => _item.MonogramBrush;

    public string? PendingNotes => _item.PendingNotes;
    public string FormattedPendingNotes => Platform.MarkdownCleaner.ToReadableText(_item.PendingNotes);
    public bool HasPendingNotes => !string.IsNullOrWhiteSpace(PendingNotes);

    public bool IsPinned => !string.IsNullOrWhiteSpace(_item.App.PinnedVersion);
    public string? PinnedVersion => _item.App.PinnedVersion;

    public string SourceBadgeText
    {
        get
        {
            if (_item.App.UpdateKind == UpdateKind.WingetPackage)
                return $"WinGet: {_item.App.WingetPackageId}";
            if (!string.IsNullOrWhiteSpace(_item.App.RepoOwner) && !string.IsNullOrWhiteSpace(_item.App.RepoName))
                return $"GitHub: {_item.App.RepoOwner}/{_item.App.RepoName}";
            return _item.App.UpdateKind.ToString();
        }
    }

    public string? RepoUrl =>
        !string.IsNullOrWhiteSpace(_item.App.RepoOwner) && !string.IsNullOrWhiteSpace(_item.App.RepoName)
            ? $"https://github.com/{_item.App.RepoOwner}/{_item.App.RepoName}"
            : null;

    public bool HasRepoUrl => !string.IsNullOrWhiteSpace(RepoUrl);

    public string? AuthorUrl =>
        !string.IsNullOrWhiteSpace(_item.App.RepoOwner)
            ? $"https://github.com/{_item.App.RepoOwner}"
            : null;

    public bool HasAuthorUrl => !string.IsNullOrWhiteSpace(AuthorUrl);

    public string? AuthorName => _item.App.RepoOwner;

    public bool CanOpenFolder =>
        !string.IsNullOrWhiteSpace(_item.App.InstallDirectory) &&
        System.IO.Directory.Exists(_item.App.InstallDirectory);

    public bool CanLaunch => _item.CanLaunchPrimary;

    [ObservableProperty]
    public partial bool AutoApplyConsent { get; set; }

    public ObservableCollection<UpdateHistoryEntry> History { get; } = [];
    public ObservableCollection<VersionArchiveEntry> Snapshots { get; } = [];

    [ObservableProperty]
    public partial bool IsLoadingReleases { get; set; }

    public ObservableCollection<ReleaseInfo> RemoteReleases { get; } = [];

    public AppDetailsViewModel(
        TrackedAppItem item,
        IReadOnlyList<UpdateHistoryEntry> history,
        Action<bool> onConsentChanged,
        IAppLauncher? launcher = null,
        VersionStore? versions = null,
        IGitHubClient? github = null)
    {
        _item = item;
        _onConsentChanged = onConsentChanged;
        _launcher = launcher ?? global::AppWatch.App.Resolve<IAppLauncher>();
        _versions = versions ?? global::AppWatch.App.Resolve<VersionStore>();
        _github = github ?? global::AppWatch.App.Resolve<IGitHubClient>();

        AutoApplyConsent = item.App.AutoApplyConsent;

        foreach (var h in history)
            History.Add(h);

        LoadLocalSnapshots();
    }

    partial void OnAutoApplyConsentChanged(bool value)
    {
        _item.App.AutoApplyConsent = value;
        _onConsentChanged(value);
    }

    private void LoadLocalSnapshots()
    {
        if (_versions is null) return;
        try
        {
            var entries = _versions.List(_item.App.Name);
            Snapshots.Clear();
            foreach (var e in entries)
                Snapshots.Add(e);
        }
        catch { /* best-effort */ }
    }

    [RelayCommand]
    public void OpenRepo()
    {
        if (string.IsNullOrWhiteSpace(RepoUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });
        }
        catch { }
    }

    [RelayCommand]
    public void OpenAuthor()
    {
        if (string.IsNullOrWhiteSpace(AuthorUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(AuthorUrl) { UseShellExecute = true });
        }
        catch { }
    }

    [RelayCommand]
    public void OpenFolder()
    {
        if (string.IsNullOrWhiteSpace(_item.App.InstallDirectory)) return;
        try
        {
            if (System.IO.Directory.Exists(_item.App.InstallDirectory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _item.App.InstallDirectory,
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    [RelayCommand]
    public void Launch()
    {
        _item.LaunchAppCommand?.Execute(_item);
    }

    [RelayCommand]
    public void TogglePin()
    {
        _item.PinCurrentCommand?.Execute(_item);
        OnPropertyChanged(nameof(IsPinned));
        OnPropertyChanged(nameof(PinnedVersion));
    }

    [RelayCommand]
    public async Task FetchRemoteReleasesAsync(CancellationToken cancellationToken = default)
    {
        if (_github is null || string.IsNullOrWhiteSpace(_item.App.RepoOwner) || string.IsNullOrWhiteSpace(_item.App.RepoName))
            return;

        IsLoadingReleases = true;
        try
        {
            var list = await _github.GetReleasesAsync(_item.App.RepoOwner, _item.App.RepoName, cancellationToken);
            RemoteReleases.Clear();
            foreach (var rel in list.Take(10))
            {
                RemoteReleases.Add(rel);
            }
        }
        catch
        {
            // ignore network issues in dialog
        }
        finally
        {
            IsLoadingReleases = false;
        }
    }
}
