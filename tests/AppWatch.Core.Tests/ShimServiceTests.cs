using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class ShimServiceTests
{
    [Fact]
    public void ShimsDirectory_IsLocatedUnderDataRoot()
    {
        var expected = Path.Combine(AppPaths.DataRoot, "shims");
        Assert.Equal(expected, ShimService.ShimsDirectory);
    }

    [Fact]
    public void CreateCmdShim_WritesValidBatchFileContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AppWatchShimTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var targetExe = @"C:\Tools\ripgrep\rg.exe";
            var shimPath = Path.Combine(tempDir, "rg.cmd");

            ShimService.WriteCmdShimFile(shimPath, targetExe);

            Assert.True(File.Exists(shimPath));
            var content = File.ReadAllText(shimPath);
            Assert.Contains("@echo off", content);
            Assert.Contains(targetExe, content);
            Assert.Contains("%*", content);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
