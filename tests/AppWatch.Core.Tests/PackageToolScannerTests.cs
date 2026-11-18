using System.Collections.Generic;
using AppWatch.Core.Domain;
using AppWatch.Infrastructure.System;
using Xunit;

namespace AppWatch.Core.Tests;

public class PackageToolScannerTests
{
    [Fact]
    public void ParseDotnetToolsOutput_ParsesValidTableSuccessfully()
    {
        var sample = """
            Идентификатор пакета      Версия           Команды        
            ----------------------------------------------------------
            dotnet-counters           10.0.750501      dotnet-counters
            vpk                       1.2.161          vpk            
            """;

        var tools = PackageToolScanner.ParseDotnetToolsOutput(sample);

        Assert.Equal(2, tools.Count);
        Assert.Equal("dotnet-counters", tools[0].PackageName);
        Assert.Equal("10.0.750501", tools[0].CurrentVersion);
        Assert.Equal(PackageRunnerType.DotnetTool, tools[0].RunnerType);

        Assert.Equal("vpk", tools[1].PackageName);
        Assert.Equal("1.2.161", tools[1].CurrentVersion);
    }

    [Fact]
    public void ParseNpmOutdatedJson_ParsesOutdatedPackagesSuccessfully()
    {
        var sampleJson = """
            {
              "npm-check-updates": {
                "current": "16.14.20",
                "wanted": "16.14.20",
                "latest": "17.1.1",
                "location": "GLOBAL"
              },
              "typescript": {
                "current": "5.3.3",
                "wanted": "5.3.3",
                "latest": "5.8.2",
                "location": "GLOBAL"
              }
            }
            """;

        var updates = PackageToolScanner.ParseNpmOutdatedJson(sampleJson);

        Assert.Equal(2, updates.Count);
        Assert.Equal("npm-check-updates", updates[0].PackageName);
        Assert.Equal("16.14.20", updates[0].CurrentVersion);
        Assert.Equal("17.1.1", updates[0].AvailableVersion);
        Assert.True(updates[0].HasUpdate);

        Assert.Equal("typescript", updates[1].PackageName);
        Assert.Equal("5.3.3", updates[1].CurrentVersion);
        Assert.Equal("5.8.2", updates[1].AvailableVersion);
    }

    [Fact]
    public void ParseBunListOutput_ParsesPackageEntriesSuccessfully()
    {
        var sample = """
            C:\Users\user\.bun\install\global node_modules (2 installed)
            ├── @opencode/cli@2.1.4
            └── turbo@1.10.0
            """;

        var tools = PackageToolScanner.ParseBunListOutput(sample);

        Assert.Equal(2, tools.Count);
        Assert.Equal("@opencode/cli", tools[0].PackageName);
        Assert.Equal("2.1.4", tools[0].CurrentVersion);
        Assert.Equal(PackageRunnerType.Bun, tools[0].RunnerType);

        Assert.Equal("turbo", tools[1].PackageName);
        Assert.Equal("1.10.0", tools[1].CurrentVersion);
    }
}
