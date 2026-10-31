using System.Text.RegularExpressions;

namespace AppWatch.Platform;

/// <summary>
/// Lightweight markdown to readable plain text cleaner for WinUI 3 controls.
/// Strips headers, links, images, code markers, HTML tags, and collapses excessive whitespace.
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
        // headers/bold/italic/code markers
        text = Regex.Replace(text, @"^#{1,6}\s*", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"\*\*?([^*\n]+)\*\*?", "$1");
        text = Regex.Replace(text, "`([^`]*)`", "$1");
        // html tags (some repos ship them)
        text = Regex.Replace(text, "<[^>]+>", "");
        // collapse 3+ blank lines
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}
