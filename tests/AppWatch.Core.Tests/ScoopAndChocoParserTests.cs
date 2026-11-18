using System.Collections.Generic;
using System.Linq;
using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class ScoopAndChocoParserTests
{
    [Fact]
    public void ParseScoopStatusOutput_ExtractsOutdatedPackages()
    {
        var sample = @"
Scoop status...
---
git 2.43.0 2.44.0
neovim 0.9.4 0.9.5
";
        var map = Infrastructure.System.PackageToolScanner.ParseScoopStatusOutput(sample);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey("git"));
        Assert.Equal("2.43.0", map["git"].CurrentVersion);
        Assert.Equal("2.44.0", map["git"].AvailableVersion);
        Assert.True(map["git"].HasUpdate);
    }

    [Fact]
    public void ParseChocolateyOutdatedOutput_ExtractsOutdatedPackages()
    {
        var sample = @"
visualstudio2026buildtools|118.11.2|118.11.3|false
git|2.42.0|2.43.0|false
";
        var map = Infrastructure.System.PackageToolScanner.ParseChocolateyOutdatedOutput(sample);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey("visualstudio2026buildtools"));
        Assert.Equal("118.11.2", map["visualstudio2026buildtools"].CurrentVersion);
        Assert.Equal("118.11.3", map["visualstudio2026buildtools"].AvailableVersion);
        Assert.True(map["visualstudio2026buildtools"].HasUpdate);
    }
}
