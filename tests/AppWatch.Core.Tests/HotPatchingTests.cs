using AppWatch.Infrastructure.System;
using Xunit;

namespace AppWatch.Core.Tests;

public class HotPatchingTests
{
    [Fact]
    public void SafeReplaceFile_WhenDestinationUnlocked_ReplacesDirectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HotPatchTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var source = Path.Combine(tempDir, "source.exe");
            var dest = Path.Combine(tempDir, "dest.exe");

            File.WriteAllText(source, "new binary content v2");
            File.WriteAllText(dest, "old binary content v1");

            var success = FileStore.SafeReplaceFile(source, dest);

            Assert.True(success);
            Assert.True(File.Exists(dest));
            Assert.Equal("new binary content v2", File.ReadAllText(dest));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void SafeReplaceFile_WhenFileLockedForExecution_UsesAtomicRenameStrategy()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HotPatchLocked_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var source = Path.Combine(tempDir, "new.exe");
            var runningApp = Path.Combine(tempDir, "locked.exe");

            File.WriteAllText(source, "updated payload v2");
            File.WriteAllText(runningApp, "running payload v1");

            // Open locked with FileShare.Read (simulates running process executable lock in Windows)
            using var fileLock = new FileStream(runningApp, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

            var replaced = FileStore.SafeReplaceFile(source, runningApp);

            Assert.True(replaced);
            Assert.True(File.Exists(runningApp));
            Assert.Equal("updated payload v2", File.ReadAllText(runningApp));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
