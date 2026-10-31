using System;
using System.IO;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using Xunit;

namespace AppWatch.Core.Tests;

public class UpdateHistoryStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly UpdateHistoryStore _store;

    public UpdateHistoryStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "appwatch-history-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _store = new UpdateHistoryStore(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void Load_ReturnsEmptyList_WhenFileMissing()
    {
        var list = _store.Load();
        Assert.Empty(list);
    }

    [Fact]
    public void AppendAndLoad_PersistsEntriesCorrectly()
    {
        var entry1 = new UpdateHistoryEntry("App1", "1.0", "1.1", DateTimeOffset.UtcNow, true, null);
        var entry2 = new UpdateHistoryEntry("App2", "2.0", "2.1", DateTimeOffset.UtcNow, false, "Network timeout");

        _store.Append(entry1);
        _store.Append(entry2);

        var loaded = _store.Load();
        Assert.Equal(2, loaded.Count);
        Assert.Equal("App1", loaded[0].AppName);
        Assert.True(loaded[0].Success);
        Assert.Equal("App2", loaded[1].AppName);
        Assert.False(loaded[1].Success);
        Assert.Equal("Network timeout", loaded[1].Detail);
    }

    [Fact]
    public void Append_EnforcesMaxEntriesRetentionCap()
    {
        for (int i = 0; i < UpdateHistoryStore.MaxEntries + 15; i++)
        {
            _store.Append(new UpdateHistoryEntry($"App{i}", "1.0", "1.1", DateTimeOffset.UtcNow, true, null));
        }

        var loaded = _store.Load();
        Assert.Equal(UpdateHistoryStore.MaxEntries, loaded.Count);
        Assert.Equal($"App{15}", loaded[0].AppName);
        Assert.Equal($"App{UpdateHistoryStore.MaxEntries + 14}", loaded[^1].AppName);
    }

    [Fact]
    public void Clear_RemovesHistoryFile()
    {
        _store.Append(new UpdateHistoryEntry("App", "1.0", "1.1", DateTimeOffset.UtcNow, true, null));
        Assert.NotEmpty(_store.Load());

        _store.Clear();
        Assert.Empty(_store.Load());
    }
}
