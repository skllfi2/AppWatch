using System;
using System.Diagnostics;
using System.IO;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.Native;
using Xunit;

namespace AppWatch.Core.Tests;

public class NativeSystemBridgeTests
{
    [Fact]
    public void IsAvailable_ReturnsTrueWhenDllPresent()
    {
        Assert.True(NativeSystemBridge.IsAvailable);
    }

    [Fact]
    public void TryInspectPe_OnCurrentProcess_Succeeds()
    {
        var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
        Assert.NotNull(currentExe);
        Assert.True(File.Exists(currentExe));

        var success = NativeSystemBridge.TryInspectPe(
            currentExe,
            out var arch,
            out var isSigned,
            out var isTrusted,
            out var fileSize);

        Assert.True(success);
        Assert.True(fileSize > 0);
        Assert.NotEqual(SystemArchitecture.Unknown, arch);
    }

    [Fact]
    public void GetLockingProcesses_OnLockedFile_FindsCurrentProcess()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var stream = new FileStream(tempFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var lockers = NativeSystemBridge.GetLockingProcesses(tempFile);
                Assert.Contains(Environment.ProcessId, lockers);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ReplaceFile_ReplacesFileContentsAtomically()
    {
        var destFile = Path.GetTempFileName();
        var srcFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(destFile, "initial destination content");
            File.WriteAllText(srcFile, "updated content from rust replace");

            var success = NativeSystemBridge.ReplaceFile(destFile, srcFile);
            Assert.True(success);
            Assert.Equal("updated content from rust replace", File.ReadAllText(destFile));
        }
        finally
        {
            if (File.Exists(destFile)) File.Delete(destFile);
            if (File.Exists(srcFile)) File.Delete(srcFile);
        }
    }

    [Fact]
    public void UnblockFile_OnPlainFile_ReturnsTrue()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var success = NativeSystemBridge.UnblockFile(tempFile);
            Assert.True(success);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
