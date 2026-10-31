using System.Net;
using System.Text.RegularExpressions;

namespace AppWatch.Core.Domain;

/// <summary>
/// Lightweight markdown to readable plain text cleaner for UI display.
/// Strips headers, links, images, code markers, HTML tags, and collapses excessive whitespace.
/// Decodes HTML entities (e.g. &amp;, &quot;) commonly found in GitHub release notes.
/// </summary>
public static class MarkdownCleaner
{
    public static string ToReadableText(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";
        var text = markdown
            .Replace("\r\n", "\n")
            .Trim();

        // images first (they'd otherwise leave the alt label behind)
        text = Regex.Replace(text, @"!\[[^\]]*\]\([^)]*\)", "");
        // links → keep the label
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]*\)", "$1");
        // headers
        text = Regex.Replace(text, @"^#{1,6}\s*", "", RegexOptions.Multiline);
        // strikethrough ~~text~~
        text = Regex.Replace(text, @"~~([^~\n]+)~~", "$1");
        // bold/italic
        text = Regex.Replace(text, @"\*\*?([^*\n]+)\*\*?", "$1");
        // inline code
        text = Regex.Replace(text, "`([^`]*)`", "$1");
        // html tags (some repos ship them)
        text = Regex.Replace(text, "<[^>]+>", "");
        // HTML entities (&amp;, &quot;, &lt;, &gt;, &#39;, etc.)
        text = WebUtility.HtmlDecode(text);
        // collapse 3+ blank lines
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}
