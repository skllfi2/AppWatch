using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Tests;

public class BackupRestoreTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Fact]
    public void FullBackup_Roundtrip_PreservesAllProperties()
    {
        var original = new AppConfig
        {
            SchemaVersion = 2,
            GitHubToken = "ghp_secret_token_12345",
            Language = "ru",
            Theme = AppTheme.Dark,
            AutoCheckEnabled = false,
            AutoApplyUpdates = true,
            CheckIntervalHours = 12,
            HealthCheckSeconds = 15,
            DefaultUpdateStrategy = UpdateStrategy.SilentInstaller,
            InstallRoot = @"D:\Tools\Apps",
            VersionCacheCount = 5,
            ToastNotificationsEnabled = false,
            SoundEffectsEnabled = false,
            ShowOnboarding = false,
            DownloadMirrors = ["https://mirror1.org/", "https://mirror2.org/"],
            TrackedApps =
            [
                new TrackedApp
                {
                    Name = "TestTool",
                    RepoOwner = "owner",
                    RepoName = "repo",
                    CurrentVersion = "v1.2.3",
                    InstallDirectory = @"D:\Tools\Apps\TestTool",
                    ExecutableName = "test.exe",
                    UpdateKind = UpdateKind.PortableZip,
                    PinnedVersion = "v1.2.3",
                    SkippedVersion = "v1.2.4",
                    Description = "A test tool",
                    Enabled = true,
                }
            ]
        };

        var json = JsonSerializer.Serialize(original, Options);
        var restored = JsonSerializer.Deserialize<AppConfig>(json, Options);

        Assert.NotNull(restored);
        Assert.Equal(original.SchemaVersion, restored.SchemaVersion);
        Assert.Equal(original.GitHubToken, restored.GitHubToken);
        Assert.Equal(original.Language, restored.Language);
        Assert.Equal(original.Theme, restored.Theme);
        Assert.Equal(original.AutoCheckEnabled, restored.AutoCheckEnabled);
        Assert.Equal(original.AutoApplyUpdates, restored.AutoApplyUpdates);
        Assert.Equal(original.CheckIntervalHours, restored.CheckIntervalHours);
        Assert.Equal(original.HealthCheckSeconds, restored.HealthCheckSeconds);
        Assert.Equal(original.DefaultUpdateStrategy, restored.DefaultUpdateStrategy);
        Assert.Equal(original.InstallRoot, restored.InstallRoot);
        Assert.Equal(original.VersionCacheCount, restored.VersionCacheCount);
        Assert.Equal(original.ToastNotificationsEnabled, restored.ToastNotificationsEnabled);
        Assert.Equal(original.SoundEffectsEnabled, restored.SoundEffectsEnabled);
        Assert.Equal(original.ShowOnboarding, restored.ShowOnboarding);
        Assert.Equal(original.DownloadMirrors, restored.DownloadMirrors);
        Assert.Single(restored.TrackedApps);

        var app = restored.TrackedApps[0];
        Assert.Equal("TestTool", app.Name);
        Assert.Equal("owner", app.RepoOwner);
        Assert.Equal("repo", app.RepoName);
        Assert.Equal("v1.2.3", app.CurrentVersion);
        Assert.Equal(@"D:\Tools\Apps\TestTool", app.InstallDirectory);
        Assert.Equal("test.exe", app.ExecutableName);
        Assert.Equal(UpdateKind.PortableZip, app.UpdateKind);
        Assert.Equal("v1.2.3", app.PinnedVersion);
        Assert.Equal("v1.2.4", app.SkippedVersion);
        Assert.Equal("A test tool", app.Description);
        Assert.True(app.Enabled);
    }

    [Fact]
    public void FullBackup_WithEmptyTrackedApps_DeserializesCorrectly()
    {
        var config = new AppConfig();
        var json = JsonSerializer.Serialize(config, Options);
        var restored = JsonSerializer.Deserialize<AppConfig>(json, Options);

        Assert.NotNull(restored);
        Assert.NotNull(restored.TrackedApps);
        Assert.Empty(restored.TrackedApps);
        Assert.Equal(3, restored.VersionCacheCount);
    }
}
