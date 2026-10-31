using System.Runtime.InteropServices;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.System;
using Xunit;

namespace AppWatch.Core.Tests;

public class ArchitectureDetectorTests
{
    [Fact]
    public void Detect_ReturnsValidSystemArchitecture()
    {
        var arch = ArchitectureDetector.Detect();

        Assert.NotEqual(SystemArchitecture.Unknown, arch);
        if (RuntimeInformation.OSArchitecture == Architecture.X64)
        {
            Assert.Equal(SystemArchitecture.X86_64, arch);
        }
        else if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
        {
            Assert.Equal(SystemArchitecture.Aarch64, arch);
        }
        else if (RuntimeInformation.OSArchitecture == Architecture.X86)
        {
            Assert.Equal(SystemArchitecture.X86, arch);
        }
    }
}
