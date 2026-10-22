using System.Runtime.Versioning;
using AppWatch.Core.Domain.Models;
using AppWatch.Core.Domain.VersionDetection;
using Xunit;

namespace AppWatch.Core.Tests;

/// <summary>
/// P3: version extracted from a data file via regex — for apps that hide
/// their version from FVI and the registry (Battle.net .product.db case).
/// </summary>
[SupportedOSPlatform("windows")]
public class VersionFileDetectionTests : IDisposable
{
    private readonly string _dir;

    public VersionFileDetectionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"appwatch-p3-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private TrackedApp App(Action<TrackedApp>? configure = null)
    {
        var app = new TrackedApp
        {
            Name = "P3 Hermetic Test", // never matches real registry entries
            RepoOwner = "external",
            RepoName = "battle-net",
            // hermetic: empty temp dir — no exe with FVI, no registry entry
            InstallDirectory = _dir,
            ExecutableName = null,
            CurrentVersion = null,
        };
        configure?.Invoke(app);
        return app;
    }

    [Fact]
    public async Task DetectAsync_VersionFile_WinsOverFvi()
    {
        // .product.db-like binary content: protobuf bytes around an ASCII version string
        // (\u escapes are exact — \x0b2 would be parsed as ONE hex escape U+00B2!)
        var file = Path.Combine(_dir, ".product.db");
        var payload = "battle.net\n\u0012\u0016C:/Program Files (x86)/Battle.netR\u000B2.53.4.17896R";
        File.WriteAllText(file, payload);

        var app = App(a =>
        {
            a.VersionFilePath = file;
            a.VersionFileRegex = @"(\d+\.\d+\.\d+\.\d+)";
        });

        var detected = await VersionDetector.DetectAsync(app, TestContext.Current.CancellationToken);

        Assert.NotNull(detected);
        Assert.Equal("2.53.4.17896", detected!.Version);
        Assert.Equal(VersionSource.VersionFile, detected.Source);
    }

    [Fact]
    public async Task DetectAsync_VersionFile_MissingFile_FallsThroughSilently()
    {
        var app = App(a =>
        {
            a.Name = "P3 Hermetic Test"; // never matches real registry entries
            a.VersionFilePath = Path.Combine(_dir, "missing.db");
            a.VersionFileRegex = @"(\d+\.\d+)";
            // no exe to detect → returns null overall
            a.InstallDirectory = null;
            a.ExecutableName = null;
        });

        var detected = await VersionDetector.DetectAsync(app, TestContext.Current.CancellationToken);

        Assert.Null(detected);
    }

    [Fact]
    public async Task DetectAsync_VersionFile_NoCaptureGroup_UsesWholeMatch()
    {
        var file = Path.Combine(_dir, "version.txt");
        File.WriteAllText(file, "version=3.2.1");

        var app = App(a =>
        {
            a.VersionFilePath = file;
            a.VersionFileRegex = @"\d+\.\d+\.\d+"; // no capture group
        });

        var detected = await VersionDetector.DetectAsync(app, TestContext.Current.CancellationToken);

        Assert.NotNull(detected);
        Assert.Equal("3.2.1", detected!.Version);
    }

    [Fact]
    public async Task DetectAsync_NoVersionFileConfigured_SkipsStep0()
    {
        var app = App(a =>
        {
            // hermetic: empty temp dir — no exe, no FVI, no registry entry
            a.InstallDirectory = _dir;
            a.ExecutableName = null;
        });

        var detected = await VersionDetector.DetectAsync(app, TestContext.Current.CancellationToken);

        Assert.Null(detected);
    }

    [Fact]
    public async Task DetectAsync_InvalidRegex_FailsSilently()
    {
        var file = Path.Combine(_dir, "v.txt");
        File.WriteAllText(file, "1.2.3");
        var app = App(a =>
        {
            a.VersionFilePath = file;
            a.VersionFileRegex = "([unclosed"; // malformed
        });

        var detected = await VersionDetector.DetectAsync(app, TestContext.Current.CancellationToken);

        Assert.Null(detected);
    }

    [Fact]
    public async Task DetectAsync_VersionFile_NonVersionCapture_Rejected()
    {
        var file = Path.Combine(_dir, "data.bin");
        File.WriteAllText(file, "build-number: 17896");

        var app = App(a =>
        {
            a.VersionFilePath = file;
            a.VersionFileRegex = @"(\d+)"; // matches a bare number — not a version
        });

        var detected = await VersionDetector.DetectAsync(app, TestContext.Current.CancellationToken);

        // sanity check (VersionRegex = \d+\.\d+...) rejects a bare number → falls through
        Assert.Null(detected);
    }
}
