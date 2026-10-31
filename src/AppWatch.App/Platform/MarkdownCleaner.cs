namespace AppWatch.Platform;

/// <summary>
/// Forwarder to core MarkdownCleaner for UI compatibility.
/// </summary>
public static class MarkdownCleaner
{
    public static string ToReadableText(string? markdown) =>
        AppWatch.Core.Domain.MarkdownCleaner.ToReadableText(markdown);
}
