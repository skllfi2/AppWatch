using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using Xunit;

namespace AppWatch.Core.Tests;

public class ChecksumParserTests
{
    private const string HashA = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string HashB = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void ParseChecksumFile_GnuFormat_StandardAndAsterisk()
    {
        var content = $"""
            {HashA}  app-windows-x64.zip
            {HashB} *setup.exe
            """;

        var map = ChecksumParser.ParseChecksumFile(content);

        Assert.Equal(2, map.Count);
        Assert.Equal(HashA, map["app-windows-x64.zip"]);
        Assert.Equal(HashB, map["setup.exe"]);
    }

    [Fact]
    public void ParseChecksumFile_BsdFormat()
    {
        var content = $"""
            SHA256 (app-v1.0-win64.zip) = {HashA}
            SHA256(setup-win.exe)={HashB}
            """;

        var map = ChecksumParser.ParseChecksumFile(content);

        Assert.Equal(2, map.Count);
        Assert.Equal(HashA, map["app-v1.0-win64.zip"]);
        Assert.Equal(HashB, map["setup-win.exe"]);
    }

    [Fact]
    public void ParseChecksumFile_ReverseColonFormat()
    {
        var content = $"""
            app-x64.zip: {HashA}
            installer.msi: {HashB}
            """;

        var map = ChecksumParser.ParseChecksumFile(content);

        Assert.Equal(2, map.Count);
        Assert.Equal(HashA, map["app-x64.zip"]);
        Assert.Equal(HashB, map["installer.msi"]);
    }

    [Fact]
    public void ParseChecksumFile_SingleBareHash()
    {
        var content = $"  {HashA}  \n";

        var map = ChecksumParser.ParseChecksumFile(content);

        Assert.Single(map);
        Assert.Equal(HashA, map["*"]);
    }

    [Fact]
    public void FindChecksumInReleaseBody_MarkdownTable()
    {
        var body = $"""
            ## Downloads and Hashes
            | File | SHA-256 |
            | --- | --- |
            | `app-windows.zip` | `{HashA}` |
            | `setup.exe` | `{HashB}` |
            """;

        var foundA = ChecksumParser.FindChecksumInReleaseBody(body, "app-windows.zip");
        var foundB = ChecksumParser.FindChecksumInReleaseBody(body, "setup.exe");

        Assert.Equal(HashA, foundA);
        Assert.Equal(HashB, foundB);
    }

    [Fact]
    public void FindChecksumInReleaseBody_BulletList()
    {
        var body = $"""
            ### Verification
            * app-x64.zip: {HashA}
            * other-os.tar.gz: {HashB}
            """;

        var found = ChecksumParser.FindChecksumInReleaseBody(body, "app-x64.zip");
        Assert.Equal(HashA, found);
    }

    [Fact]
    public void FindCompanionChecksumAsset_PrioritizesSpecificOverCollective()
    {
        var assets = new List<ReleaseAsset>
        {
            new("app-x64.zip", "https://example.com/app.zip", 1000, null),
            new("checksums.txt", "https://example.com/checksums.txt", 100, null),
            new("app-x64.zip.sha256", "https://example.com/app-x64.zip.sha256", 64, null),
        };

        var found = ChecksumParser.FindCompanionChecksumAsset(assets, "app-x64.zip");

        Assert.NotNull(found);
        Assert.Equal("app-x64.zip.sha256", found!.Name);
    }

    [Fact]
    public void FindCompanionChecksumAsset_FallsBackToCollectiveChecksums()
    {
        var assets = new List<ReleaseAsset>
        {
            new("app-x64.zip", "https://example.com/app.zip", 1000, null),
            new("SHA256SUMS", "https://example.com/SHA256SUMS", 200, null),
        };

        var found = ChecksumParser.FindCompanionChecksumAsset(assets, "app-x64.zip");

        Assert.NotNull(found);
        Assert.Equal("SHA256SUMS", found!.Name);
    }

    [Fact]
    public void FindCompanionChecksumAsset_ReturnsNullWhenNonePresent()
    {
        var assets = new List<ReleaseAsset>
        {
            new("app-x64.zip", "https://example.com/app.zip", 1000, null),
            new("app-linux.tar.gz", "https://example.com/app.tar.gz", 1000, null),
        };

        var found = ChecksumParser.FindCompanionChecksumAsset(assets, "app-x64.zip");
        Assert.Null(found);
    }
}
