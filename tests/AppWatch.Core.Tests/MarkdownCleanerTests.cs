using AppWatch.Core.Domain;
using Xunit;

namespace AppWatch.Core.Tests;

public class MarkdownCleanerTests
{
    [Fact]
    public void ToReadableText_StripsHeadersAndImages()
    {
        var md = "# Header 1\n## Header 2\n![Screenshot](https://example.com/pic.png)\nSome text here.";
        var result = MarkdownCleaner.ToReadableText(md);

        Assert.DoesNotContain("#", result);
        Assert.DoesNotContain("Screenshot", result);
        Assert.DoesNotContain("https://example.com/pic.png", result);
        Assert.Contains("Header 1", result);
        Assert.Contains("Some text here.", result);
    }

    [Fact]
    public void ToReadableText_PreservesLinkText()
    {
        var md = "Download the latest version from [GitHub Releases](https://github.com/user/repo/releases).";
        var result = MarkdownCleaner.ToReadableText(md);

        Assert.Equal("Download the latest version from GitHub Releases.", result);
    }

    [Fact]
    public void ToReadableText_DecodesHtmlEntities()
    {
        var md = "Fix: &quot;quoted string&quot; &amp; &lt;brackets&gt;";
        var result = MarkdownCleaner.ToReadableText(md);

        Assert.Equal("Fix: \"quoted string\" & <brackets>", result);
    }

    [Fact]
    public void ToReadableText_StripsInlineCodeAndHtmlTags()
    {
        var md = "Use `git pull` or <span style=\"color:red\">npm update</span> for changes.";
        var result = MarkdownCleaner.ToReadableText(md);

        Assert.Equal("Use git pull or npm update for changes.", result);
    }

    [Fact]
    public void ToReadableText_HandlesNullOrWhitespace()
    {
        Assert.Equal("", MarkdownCleaner.ToReadableText(null));
        Assert.Equal("", MarkdownCleaner.ToReadableText("   "));
    }
}
