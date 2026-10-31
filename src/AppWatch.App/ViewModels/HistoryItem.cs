using AppWatch.Core.Domain.Models;
using AppWatch.Localization;
using Microsoft.UI.Xaml.Media;

namespace AppWatch.ViewModels;

/// <summary>
/// Row VM for the history list: everything the DataTemplate needs lives here
/// (x:Bind inside DataTemplate resolves only against this type).
/// </summary>
public sealed class HistoryItem
{
    public UpdateHistoryEntry Entry { get; }

    public HistoryItem(UpdateHistoryEntry entry) => Entry = entry;

    public string AppName => Entry.AppName;

    public string DateText => Entry.AtUtc.LocalDateTime.ToString("dd.MM.yyyy HH:mm");

    public string VersionText => $"{Entry.FromVersion} → {Entry.ToVersion}";

    public bool IsSuccess => Entry.Success;

    public string ResultText => Entry.Success
        ? LocalizationService.Instance["HistorySuccess"]
        : LocalizationService.Instance["HistoryFailed"];

    public string? Detail => Entry.Detail;

    public string? ReleaseNotes => Entry.ReleaseNotes;
    public bool HasReleaseNotes => !string.IsNullOrWhiteSpace(Entry.ReleaseNotes);
    public string ReleaseNotesText => Platform.MarkdownCleaner.ToReadableText(Entry.ReleaseNotes);
    public string DetailsNotesText => LocalizationService.Instance["DetailsNotes"];

    public Brush StatusBrush => Entry.Success
        ? MakeBrush(0x2E, 0xA0, 0x43)
        : MakeBrush(0xD8, 0x3B, 0x01);

    public Brush MonogramBrush
    {
        get
        {
            var hash = (uint)Math.Abs(string.GetHashCode(AppName, StringComparison.OrdinalIgnoreCase));
            var r = (byte)(0x50 + hash % 0x40);
            var g = (byte)(hash >> 4 & 0x7F);
            var b = (byte)(hash >> 8 & 0x9F);
            return MakeBrush(r, g, b);
        }
    }

    public string Monogram => string.IsNullOrEmpty(AppName) ? "?" : AppName[..1].ToUpperInvariant();

    private static SolidColorBrush MakeBrush(byte r, byte g, byte b) =>
        new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, r, g, b));
}
