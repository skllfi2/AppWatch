using System;
using System.IO;
using System.Linq;
using AppWatch.Infrastructure.System;
using Xunit;

namespace AppWatch.Core.Tests;

public class VersionStoreTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly VersionStore _store;

    public VersionStoreTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "appwatch-verstore-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _store = new VersionStore(keepCount: 2, rootDirectory: _tempRoot);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true); } catch { }
    }

    [Theory]
    [InlineData("v1.2.3", "v1.2.3")]
    [InlineData("Node.js", "Node.js")]
    [InlineData("7-Zip", "7-Zip")]
    [InlineData("Xiaomi MiMo AI", "Xiaomi MiMo AI")]
    [InlineData("My:Bad/Path*Name?", "My_Bad_Path_Name_")]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    public void Sanitize_PreservesDotsAndHyphensWhileCleaningInvalidChars(string? input, string expected)
    {
        var sanitized = VersionStore.Sanitize(input!);
        Assert.Equal(expected, sanitized);
    }

    [Fact]
    public void ArchiveBackup_MovesBackupToArchive_AndPrunesOldVersions()
    {
        var appInstallDir = Path.Combine(_tempRoot, "AppLive");
        var appBackupDir = appInstallDir + ".old";

        // Create backup 1
        Directory.CreateDirectory(appBackupDir);
        File.WriteAllText(Path.Combine(appBackupDir, "app.exe"), "v1.0 binary");
        _store.ArchiveBackup(appInstallDir, "TestApp", "v1.0");

        var list1 = _store.List("TestApp");
        Assert.Single(list1);
        Assert.Equal("v1.0", list1[0].Label);

        // Create backup 2
        Directory.CreateDirectory(appBackupDir);
        File.WriteAllText(Path.Combine(appBackupDir, "app.exe"), "v1.1 binary");
        _store.ArchiveBackup(appInstallDir, "TestApp", "v1.1");

        var list2 = _store.List("TestApp");
        Assert.Equal(2, list2.Count);

        // Create backup 3 — keepCount is 2, so oldest (v1.0) must be pruned
        Directory.CreateDirectory(appBackupDir);
        File.WriteAllText(Path.Combine(appBackupDir, "app.exe"), "v1.2 binary");
        _store.ArchiveBackup(appInstallDir, "TestApp", "v1.2");

        var list3 = _store.List("TestApp");
        Assert.Equal(2, list3.Count);
        Assert.Contains(list3, x => x.Label == "v1.2");
        Assert.Contains(list3, x => x.Label == "v1.1");
        Assert.DoesNotContain(list3, x => x.Label == "v1.0");
    }

    [Fact]
    public void RollbackTo_RestoresSelectedVersion_AndArchivesCurrent()
    {
        var appInstallDir = Path.Combine(_tempRoot, "LiveApp");
        var appBackupDir = appInstallDir + ".old";

        // Setup archived snapshot v1.0
        Directory.CreateDirectory(appBackupDir);
        File.WriteAllText(Path.Combine(appBackupDir, "app.txt"), "old content");
        _store.ArchiveBackup(appInstallDir, "MyTool", "v1.0");

        // Current live app has "new content"
        Directory.CreateDirectory(appInstallDir);
        File.WriteAllText(Path.Combine(appInstallDir, "app.txt"), "new content");

        var snapshot = _store.List("MyTool").First(x => x.Label == "v1.0");

        // Perform rollback
        _store.RollbackTo(appInstallDir, "MyTool", snapshot);

        // Verify live install has old content restored
        Assert.True(File.Exists(Path.Combine(appInstallDir, "app.txt")));
        Assert.Equal("old content", File.ReadAllText(Path.Combine(appInstallDir, "app.txt")));

        // Verify current (new content) was preserved as a rollback candidate
        var afterList = _store.List("MyTool");
        Assert.Contains(afterList, x => x.Label.StartsWith("current-"));
    }

    [Fact]
    public void GetTotalSizeBytes_CalculatesSizeAccurately()
    {
        var appInstallDir = Path.Combine(_tempRoot, "App");
        var appBackupDir = appInstallDir + ".old";

        Directory.CreateDirectory(appBackupDir);
        File.WriteAllBytes(Path.Combine(appBackupDir, "payload.bin"), new byte[4096]);
        _store.ArchiveBackup(appInstallDir, "SizedApp", "v1.0");

        var totalBytes = _store.GetTotalSizeBytes();
        Assert.True(totalBytes >= 4096);
    }
}
