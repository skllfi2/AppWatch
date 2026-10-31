using System;
using System.IO;
using AppWatch.Infrastructure.System;
using Xunit;

namespace AppWatch.Core.Tests;

public class FileStoreCrossVolumeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileStore _fileStore;

    public FileStoreCrossVolumeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "appwatch-filestore-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _fileStore = new FileStore();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { }
    }

    [Fact]
    public void MoveDirectory_MovesFilesAndSubdirectories()
    {
        var src = Path.Combine(_tempDir, "src");
        var dest = Path.Combine(_tempDir, "nested", "dest");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "file.txt"), "hello");
        var sub = Path.Combine(src, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "subfile.txt"), "world");

        FileStore.MoveDirectory(src, dest);

        Assert.False(Directory.Exists(src));
        Assert.True(Directory.Exists(dest));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(dest, "file.txt")));
        Assert.Equal("world", File.ReadAllText(Path.Combine(dest, "sub", "subfile.txt")));
    }

    [Fact]
    public void CopyDirectoryRecursive_CopiesAllContents()
    {
        var src = Path.Combine(_tempDir, "copy_src");
        var dest = Path.Combine(_tempDir, "copy_dest");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.txt"), "content a");
        var sub = Path.Combine(src, "child");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "b.txt"), "content b");

        FileStore.CopyDirectoryRecursive(src, dest);

        Assert.True(Directory.Exists(src));
        Assert.True(Directory.Exists(dest));
        Assert.Equal("content a", File.ReadAllText(Path.Combine(dest, "a.txt")));
        Assert.Equal("content b", File.ReadAllText(Path.Combine(dest, "child", "b.txt")));
    }

    [Fact]
    public void ReplaceWithRollback_WhenSuccessful_ReplacesDirectory()
    {
        var contentRoot = Path.Combine(_tempDir, "staged_content");
        var installDir = Path.Combine(_tempDir, "install_dir");
        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "old.txt"), "old");

        Directory.CreateDirectory(contentRoot);
        File.WriteAllText(Path.Combine(contentRoot, "new.txt"), "new");

        _fileStore.ReplaceWithRollback(contentRoot, installDir);

        Assert.True(File.Exists(Path.Combine(installDir, "new.txt")));
        Assert.False(File.Exists(Path.Combine(installDir, "old.txt")));
        Assert.True(Directory.Exists(installDir + ".old"));
    }

    [Fact]
    public void RestoreBackup_RestoresPreviousInstallation()
    {
        var installDir = Path.Combine(_tempDir, "app_to_restore");
        var backupDir = installDir + ".old";

        Directory.CreateDirectory(installDir);
        File.WriteAllText(Path.Combine(installDir, "corrupted.txt"), "bad");

        Directory.CreateDirectory(backupDir);
        File.WriteAllText(Path.Combine(backupDir, "original.txt"), "good");

        _fileStore.RestoreBackup(installDir);

        Assert.True(File.Exists(Path.Combine(installDir, "original.txt")));
        Assert.False(File.Exists(Path.Combine(installDir, "corrupted.txt")));
    }
}
