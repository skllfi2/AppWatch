using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Core.Tests;

public class BatchImportAndScannerTests
{
    [Fact]
    public void BatchImport_ParsesVariousFormats()
    {
        var lines = new[]
        {
            "https://github.com/git-for-windows/git",
            "BurntSushi/ripgrep",
            "winget:Microsoft.VisualStudioCode",
            "7zip.7zip",
            "# This is a comment",
            "   ",
            "invalid line with many spaces"
        };

        var parsed = new List<(string Name, string? Owner, string? Repo, string? WingetId)>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            if (line.Contains("github.com/"))
            {
                var m = System.Text.RegularExpressions.Regex.Match(line, @"github\.com[/:]([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+)");
                if (m.Success)
                    parsed.Add((m.Groups[2].Value, m.Groups[1].Value, m.Groups[2].Value, null));
            }
            else if (line.Contains('/') && line.Split('/').Length == 2)
            {
                var parts = line.Split('/');
                parsed.Add((parts[1], parts[0], parts[1], null));
            }
            else if (line.StartsWith("winget:", StringComparison.OrdinalIgnoreCase))
            {
                var id = line["winget:".Length..].Trim();
                parsed.Add((id, null, null, id));
            }
            else if (line.Contains('.') && !line.Contains(' '))
            {
                parsed.Add((line, null, null, line));
            }
        }

        Assert.Equal(4, parsed.Count);
        Assert.Equal("git-for-windows", parsed[0].Owner);
        Assert.Equal("git", parsed[0].Repo);
        Assert.Equal("BurntSushi", parsed[1].Owner);
        Assert.Equal("ripgrep", parsed[1].Repo);
        Assert.Equal("Microsoft.VisualStudioCode", parsed[2].WingetId);
        Assert.Equal("7zip.7zip", parsed[3].WingetId);
    }

    [Fact]
    public void SystemScanner_CatalogMatchingAndExclusion()
    {
        var catalog = new List<CatalogEntry>
        {
            new() { Name = "Git", RepoOwner = "git-for-windows", RepoName = "git", UpdateKind = UpdateKind.Installer, ExecutableName = "git.exe" },
            new() { Name = "7-Zip", RepoOwner = "ip7z", RepoName = "7zip", UpdateKind = UpdateKind.Installer, WingetPackageId = "7zip.7zip" }
        };

        var installedDisplayNames = new[]
        {
            "Git version 2.45.1.windows.1",
            "Security Update for Windows (KB5034441)",
            "Microsoft Visual C++ 2015-2022 Redistributable (x64)",
            "7-Zip 24.05 (x64)"
        };

        var matches = new List<CatalogEntry>();

        foreach (var name in installedDisplayNames)
        {
            // Noise filter
            var lower = name.ToLowerInvariant();
            if (lower.StartsWith("security update") || lower.Contains("visual c++"))
                continue;

            var match = catalog.FirstOrDefault(c =>
                name.Contains(c.Name, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                matches.Add(match);
        }

        Assert.Equal(2, matches.Count);
        Assert.Equal("Git", matches[0].Name);
        Assert.Equal("7-Zip", matches[1].Name);
    }
}
