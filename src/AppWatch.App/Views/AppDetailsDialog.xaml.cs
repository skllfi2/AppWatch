using System.Text.RegularExpressions;

using AppWatch.Core.Domain.Models;

using AppWatch.Infrastructure;

using AppWatch.Localization;

using AppWatch.ViewModels;

using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>

/// Layer-1 UI: per-app details — versions, pending release notes (markdown body,

/// stripped to text), last history entries, automation consent toggle.

/// Primary = update (only when a pending update exists), Secondary = rollback

/// (only when the version cache has snapshots). Results handled by MainViewModel.

/// Built in C# — XAML ContentDialogs crash the compiler on RU locale.

/// </summary>

public sealed class AppDetailsDialog : ContentDialog

{

    private readonly TrackedAppItem _item;

    private readonly Action<bool> _onConsentChanged;

    public AppDetailsDialog(

        TrackedAppItem item,

        IReadOnlyList<UpdateHistoryEntry> history,

        Action<bool> onConsentChanged)

    {

        _item = item;

        _onConsentChanged = onConsentChanged;

        var loc = LocalizationService.Instance;

        Title = item.Name;

        CloseButtonText = loc["DetailsClose"];

        DefaultButton = ContentDialogButton.Primary;

        MinWidth = 520;

        if (item.UpdateAvailable)

            PrimaryButtonText = loc["BtnUpdateOne"];

        if (item.RollbackAvailable)

            SecondaryButtonText = loc["BtnRollback"];

        Content = BuildContent(history, loc);

    }

    private StackPanel BuildContent(IReadOnlyList<UpdateHistoryEntry> history, LocalizationService loc)

    {

        var panel = new StackPanel { Spacing = 14, MaxWidth = 520 };

        // ── Description ───────────────────────────────────────────

        if (!string.IsNullOrWhiteSpace(_item.App.Description))

        {

            panel.Children.Add(new TextBlock

            {

                Text = _item.App.Description,

                FontSize = 13,

                Opacity = 0.75,

                TextWrapping = TextWrapping.Wrap,

                Margin = new Thickness(0, -6, 0, 4),

            });

        }

        // ── Versions ──────────────────────────────────────────────

        panel.Children.Add(SectionHeader(loc["DetailsVersions"], "\uE8F1"));

        panel.Children.Add(new TextBlock

        {

            Text = _item.UpdateAvailable && _item.AvailableVersion is { } to

                ? string.Format(loc["DetailsInstalledToAvailable"], _item.Version, to)

                : string.Format(loc["DetailsInstalledOnly"], _item.Version),

            FontSize = 13,

            Opacity = 0.85,

            TextWrapping = TextWrapping.Wrap,

        });

        // ── What's new (pending release notes) ─────────────────────

        if (_item.UpdateAvailable)

        {

            panel.Children.Add(SectionHeader(loc["DetailsNotes"], "\uE70F"));

            var notes = Platform.MarkdownCleaner.ToReadableText(_item.PendingNotes);

            var notesContainer = new Border

            {

                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundBrush"],

                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BorderDefaultBrush"],

                BorderThickness = new Thickness(1),

                CornerRadius = new CornerRadius(8),

                Padding = new Thickness(12, 10, 12, 10),

                Child = string.IsNullOrWhiteSpace(notes)

                    ? new TextBlock

                    {

                        Text = loc["DetailsNoNotes"],

                        FontSize = 12,

                        Opacity = 0.55,

                        TextWrapping = TextWrapping.Wrap,

                    }

                    : new ScrollViewer

                    {

                        Content = new TextBlock

                        {

                            Text = notes,

                            FontSize = 12,

                            Opacity = 0.85,

                            TextWrapping = TextWrapping.Wrap,

                        },

                        MaxHeight = 220,

                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,

                    }

            };

            panel.Children.Add(notesContainer);

        }

        // ── Automation consent ────────────────────────────────────

        panel.Children.Add(SectionHeader(loc["DetailsAutomation"], "\uE77B"));

        var consent = new ToggleSwitch

        {

            Header = loc["DetailsConsent"],

            OnContent = loc["On"],

            OffContent = loc["Off"],

            IsOn = _item.App.AutoApplyConsent,

        };

        consent.Toggled += (_, _) => _onConsentChanged(consent.IsOn);

        var consentContainer = new Border

        {

            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundBrush"],

            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BorderDefaultBrush"],

            BorderThickness = new Thickness(1),

            CornerRadius = new CornerRadius(8),

            Padding = new Thickness(14, 10, 14, 10),

            Child = new StackPanel

            {

                Spacing = 4,

                Children =

                {

                    consent,

                    new TextBlock

                    {

                        Text = loc["DetailsConsentHint"],

                        FontSize = 11,

                        Opacity = 0.6,

                        TextWrapping = TextWrapping.Wrap,

                    }

                }

            }

        };

        panel.Children.Add(consentContainer);

        // ── History (last entries for this app) ────────────────────

        if (history.Count > 0)

        {

            panel.Children.Add(SectionHeader(loc["DetailsHistory"], "\uE81C"));

            foreach (var h in history)

            {

                var line = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 6) };

                line.Children.Add(new TextBlock

                {

                    Text = $"{h.FromVersion} → {h.ToVersion}  ·  {h.AtUtc.ToLocalTime():dd.MM HH:mm}",

                    FontSize = 12,

                    Opacity = 0.8,

                });

                line.Children.Add(new TextBlock

                {

                    Text = (h.Success ? "✓" : "✗ ") + (h.Detail ?? (h.Success ? loc["Updated"] : loc["Error"])),

                    FontSize = 11,

                    Opacity = h.Success ? 0.6 : 0.85,

                    TextWrapping = TextWrapping.Wrap,

                });

                if (!string.IsNullOrWhiteSpace(h.ReleaseNotes))

                {

                    var expander = new Expander

                    {

                        Header = new TextBlock

                        {

                            Text = loc["DetailsNotes"],

                            FontSize = 11,

                            Opacity = 0.7,

                        },

                        Content = new ScrollViewer

                        {

                            Content = new TextBlock

                            {

                                Text = Platform.MarkdownCleaner.ToReadableText(h.ReleaseNotes),

                                FontSize = 11,

                                Opacity = 0.8,

                                TextWrapping = TextWrapping.Wrap,

                            },

                            MaxHeight = 150,

                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,

                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,

                        },

                        Margin = new Thickness(0, 4, 0, 4),

                    };

                    line.Children.Add(expander);

                }

                panel.Children.Add(line);

            }

        }

        return panel;

    }

    private static StackPanel SectionHeader(string text, string glyph)

    {

        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 2) };

        sp.Children.Add(new FontIcon

        {

            Glyph = glyph,

            FontSize = 12,

            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentBrush"],

            VerticalAlignment = VerticalAlignment.Center

        });

        sp.Children.Add(new TextBlock

        {

            Text = text,

            FontSize = 12,

            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,

            VerticalAlignment = VerticalAlignment.Center

        });

        return sp;

    }

    /// <summary>Lightweight markdown → readable text (headers, links, images, emphasis).</summary>

    private static string MarkdownToText(string? markdown)

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

