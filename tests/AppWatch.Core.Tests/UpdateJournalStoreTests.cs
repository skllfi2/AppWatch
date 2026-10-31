using System;
using System.IO;
using AppWatch.Core.Domain;
using AppWatch.Infrastructure;
using Xunit;

namespace AppWatch.Core.Tests;

public class UpdateJournalStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _journalPath;
    private readonly UpdateJournalStore _store;

    public UpdateJournalStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "appwatch-journal-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _journalPath = Path.Combine(_tempDir, "pending.json");
        _store = new UpdateJournalStore(_journalPath);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileDoesNotExist()
    {
        Assert.Null(_store.Load());
    }

    [Fact]
    public void WriteAndLoad_RoundTripsPendingUpdate()
    {
        var pending = new PendingUpdate("Telegram", @"C:\Apps\Telegram", "4.15.0", "4.16.0", DateTimeOffset.UtcNow);
        _store.Write(pending);

        var loaded = _store.Load();
        Assert.NotNull(loaded);
        Assert.Equal("Telegram", loaded.AppName);
        Assert.Equal(@"C:\Apps\Telegram", loaded.InstallDirectory);
        Assert.Equal("4.15.0", loaded.FromVersion);
        Assert.Equal("4.16.0", loaded.ToVersion);
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileIsCorrupted()
    {
        File.WriteAllText(_journalPath, "{ invalid json content !!!");
        var loaded = _store.Load();
        Assert.Null(loaded);
    }

    [Fact]
    public void Clear_RemovesJournalFile()
    {
        var pending = new PendingUpdate("App", @"C:\App", "1.0", "1.1", DateTimeOffset.UtcNow);
        _store.Write(pending);
        Assert.True(File.Exists(_journalPath));

        _store.Clear();
        Assert.False(File.Exists(_journalPath));
        Assert.Null(_store.Load());
    }
}
