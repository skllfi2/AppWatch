using System.Diagnostics;
using System.IO.Compression;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.Config;
using AppWatch.Infrastructure.System;

namespace AppWatch.Core.Tests;

public sealed class AuditHardeningTests : IDisposable
{
    private readonly string _tempDir;

    public AuditHardeningTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "appwatch-audit-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch
        {
            // best effort test cleanup
        }
    }

    [Fact]
    public void FileStore_ExtractToStaging_SanitizesTagWithInvalidPathCharacters_ForSingleExe()
    {
        var fileStore = new FileStore();
        var dummyExe = Path.Combine(_tempDir, "sample_v1.0_x64.exe");
        File.WriteAllText(dummyExe, "MZ_DUMMY_BINARY");

        // Tag with slashes, colons, asterisks, and questions marks that would break Path.Combine / Directory.CreateDirectory
        var dangerousTag = "release/v1.0.0:preview*?<win64>";
        var stagingDir = fileStore.ExtractToStaging(dummyExe, dangerousTag, "sample.exe");

        try
        {
            Assert.True(Directory.Exists(stagingDir));
            var targetExe = Path.Combine(stagingDir, "sample.exe");
            Assert.True(File.Exists(targetExe));
            Assert.Equal("MZ_DUMMY_BINARY", File.ReadAllText(targetExe));
        }
        finally
        {
            try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true); } catch { }
        }
    }

    [Fact]
    public void FileStore_ExtractToStaging_SanitizesTagWithInvalidPathCharacters_ForZipArchive()
    {
        var fileStore = new FileStore();
        var dummyZip = Path.Combine(_tempDir, "test_pkg.zip");

        using (var archive = ZipFile.Open(dummyZip, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("payload.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("archived_payload");
        }

        var dangerousTag = "feature/branch:tag?v=2";
        var stagingDir = fileStore.ExtractToStaging(dummyZip, dangerousTag);

        try
        {
            Assert.True(Directory.Exists(stagingDir));
            var extractedPayload = Path.Combine(stagingDir, "payload.txt");
            Assert.True(File.Exists(extractedPayload));
            Assert.Equal("archived_payload", File.ReadAllText(extractedPayload));
        }
        finally
        {
            try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true); } catch { }
        }
    }

    [Fact]
    public void JsonConfigStore_CorruptedJson_CreatesBackupAndReturnsDefaultConfig()
    {
        var store = new JsonConfigStore(_tempDir);
        var configFile = Path.Combine(_tempDir, "config.json");
        const string brokenJson = "{ \"schemaVersion\": 2, \"trackedApps\": [ INVALID_JSON_PAYLOAD ...";
        File.WriteAllText(configFile, brokenJson);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Empty(loaded.TrackedApps);

        var corruptedFiles = Directory.GetFiles(_tempDir, "config.json.corrupted.*");
        Assert.Single(corruptedFiles);
        Assert.Equal(brokenJson, File.ReadAllText(corruptedFiles[0]));
    }

    [Fact]
    public void JsonConfigStore_ValidJson_LoadsSuccessfullyWithoutCreatingCorruptedBackup()
    {
        var store = new JsonConfigStore(_tempDir);
        var validConfig = new AppConfig
        {
            GitHubToken = "ghp_valid_test_token",
            Language = "ru",
            TrackedApps = [new TrackedApp { Name = "Tool", RepoName = "repo", RepoOwner = "owner" }]
        };
        store.Save(validConfig);

        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal("ghp_valid_test_token", loaded.GitHubToken);
        Assert.Equal("ru", loaded.Language);
        Assert.Single(loaded.TrackedApps);
        Assert.Equal("Tool", loaded.TrackedApps[0].Name);

        var corruptedFiles = Directory.GetFiles(_tempDir, "config.json.corrupted.*");
        Assert.Empty(corruptedFiles);
    }

    [Fact]
    public void ProcessService_IsRunning_ReturnsTrueForCurrentProcess_AndFalseForNonExistent()
    {
        var processService = new ProcessService();
        var currentProc = Process.GetCurrentProcess();

        var isCurrentRunning = processService.IsRunning(currentProc.ProcessName);
        var isFakeRunning = processService.IsRunning("AppWatch_NonExistent_Proc_" + Guid.NewGuid().ToString("N"));

        Assert.True(isCurrentRunning);
        Assert.False(isFakeRunning);
    }

    [Fact]
    public void ProcessService_Kill_NonExistentProcess_ReturnsTrueGracefully()
    {
        var processService = new ProcessService();
        var fakeName = "AppWatch_NonExistent_Proc_" + Guid.NewGuid().ToString("N");

        var result = processService.Kill(fakeName);

        Assert.True(result);
    }
}
