using System.Text.RegularExpressions;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure;
using AppWatch.Localization;
using AppWatch.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// MVVM-compliant per-app details and control hub dialog.
/// Displays app metadata, source badges, direct links to repository and author,
/// executable launch, folder explorer, local snapshots & rollback, GitHub history,
/// pending notes and automation preferences.
/// Built in C# to ensure zero XAML compiler crashes on regional Windows locales.
/// </summary>
public sealed class AppDetailsDialog : ContentDialog
{
    public AppDetailsViewModel ViewModel { get; }

    public AppDetailsDialog(
        TrackedAppItem item,
        IReadOnlyList<UpdateHistoryEntry> history,
        Action<bool> onConsentChanged)
    {
        ViewModel = new AppDetailsViewModel(item, history, onConsentChanged);

        var loc = LocalizationService.Instance;
        Title = ViewModel.Name;
        CloseButtonText = loc["DetailsClose"];
        DefaultButton = ContentDialogButton.Primary;
        MinWidth = 560;

        if (ViewModel.UpdateAvailable)
            PrimaryButtonText = loc["BtnUpdateOne"];

        if (ViewModel.RollbackAvailable)
            SecondaryButtonText = loc["BtnRollback"];

        Content = BuildContent(loc);
    }

    private ScrollViewer BuildContent(LocalizationService loc)
    {
        var panel = new StackPanel { Spacing = 14, MaxWidth = 560 };

        // ── Header Card: Monogram/Icon + Description + Source badge ──────
        var headerCard = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BorderDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12)
        };

        var headerGrid = new Grid { ColumnSpacing = 12 };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Icon / Monogram
        var iconContainer = new Grid
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(8),
            VerticalAlignment = VerticalAlignment.Top
        };
        if (ViewModel.Icon is not null)
        {
            iconContainer.Children.Add(new Image
            {
                Source = ViewModel.Icon,
                Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
            });
        }
        else
        {
            var monogramBorder = new Border
            {
                Background = ViewModel.MonogramBrush,
                CornerRadius = new CornerRadius(8),
                Child = new TextBlock
                {
                    Text = ViewModel.Monogram,
                    FontSize = 18,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            iconContainer.Children.Add(monogramBorder);
        }
        Grid.SetColumn(iconContainer, 0);
        headerGrid.Children.Add(iconContainer);

        var headerInfo = new StackPanel { Spacing = 4 };
        if (!string.IsNullOrWhiteSpace(ViewModel.Description))
        {
            headerInfo.Children.Add(new TextBlock
            {
                Text = ViewModel.Description,
                FontSize = 13,
                Opacity = 0.85,
                TextWrapping = TextWrapping.Wrap
            });
        }

        // Source badge & external links
        var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        var sourceBadge = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HeroOverlayBrush"],
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = ViewModel.SourceBadgeText,
                FontSize = 11,
                Opacity = 0.8
            }
        };
        metaRow.Children.Add(sourceBadge);

        if (ViewModel.HasRepoUrl)
        {
            var btnRepo = new HyperlinkButton
            {
                Content = loc["DetailsOpenRepo"],
                FontSize = 11,
                Padding = new Thickness(4, 0, 4, 0)
            };
            btnRepo.Click += (_, _) => ViewModel.OpenRepoCommand.Execute(null);
            metaRow.Children.Add(btnRepo);
        }

        if (ViewModel.HasAuthorUrl)
        {
            var btnAuthor = new HyperlinkButton
            {
                Content = loc["DetailsOpenAuthor"],
                FontSize = 11,
                Padding = new Thickness(4, 0, 4, 0)
            };
            btnAuthor.Click += (_, _) => ViewModel.OpenAuthorCommand.Execute(null);
            metaRow.Children.Add(btnAuthor);
        }

        headerInfo.Children.Add(metaRow);
        Grid.SetColumn(headerInfo, 1);
        headerGrid.Children.Add(headerInfo);

        headerCard.Child = headerGrid;
        panel.Children.Add(headerCard);

        // ── Action Bar: Launch, Open Folder, Pin ───────────────────────────
        var actionBar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        if (ViewModel.CanLaunch)
        {
            var btnLaunch = new Button
            {
                Padding = new Thickness(12, 6, 12, 6),
                CornerRadius = new CornerRadius(6),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HeroOverlayBrush"],
                BorderThickness = new Thickness(0),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new FontIcon { Glyph = "\uE768", FontSize = 12 },
                        new TextBlock { Text = loc["BtnLaunch"], FontSize = 12 }
                    }
                }
            };
            btnLaunch.Click += (_, _) => ViewModel.LaunchCommand.Execute(null);
            actionBar.Children.Add(btnLaunch);
        }

        if (ViewModel.CanOpenFolder)
        {
            var btnFolder = new Button
            {
                Padding = new Thickness(12, 6, 12, 6),
                CornerRadius = new CornerRadius(6),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HeroOverlayBrush"],
                BorderThickness = new Thickness(0),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children =
                    {
                        new FontIcon { Glyph = "\uED25", FontSize = 12 },
                        new TextBlock { Text = loc["DetailsOpenFolder"], FontSize = 12 }
                    }
                }
            };
            btnFolder.Click += (_, _) => ViewModel.OpenFolderCommand.Execute(null);
            actionBar.Children.Add(btnFolder);
        }

        var btnPin = new Button
        {
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(6),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["HeroOverlayBrush"],
            BorderThickness = new Thickness(0),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new FontIcon { Glyph = "\uE840", FontSize = 12 },
                    new TextBlock
                    {
                        Text = ViewModel.IsPinned ? loc["DetailsUnpinVersion"] : loc["DetailsPinVersion"],
                        FontSize = 12
                    }
                }
            }
        };
        btnPin.Click += (_, _) =>
        {
            ViewModel.TogglePinCommand.Execute(null);
            if (btnPin.Content is StackPanel sp && sp.Children[1] is TextBlock tb)
            {
                tb.Text = ViewModel.IsPinned ? loc["DetailsUnpinVersion"] : loc["DetailsPinVersion"];
            }
        };
        actionBar.Children.Add(btnPin);

        panel.Children.Add(actionBar);

        // ── Versions & Rollback Snapshots ─────────────────────────
        panel.Children.Add(SectionHeader(loc["DetailsVersions"], "\uE8F1"));

        var versionBlock = new TextBlock
        {
            Text = ViewModel.UpdateAvailable && ViewModel.AvailableVersion is { } to
                ? string.Format(loc["DetailsInstalledToAvailable"], ViewModel.Version, to)
                : string.Format(loc["DetailsInstalledOnly"], ViewModel.Version),
            FontSize = 13,
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap
        };
        panel.Children.Add(versionBlock);

        // Local snapshots for rollback
        if (ViewModel.Snapshots.Count > 0)
        {
            var snapExpander = new Expander
            {
                Header = new TextBlock
                {
                    Text = $"{loc["DetailsLocalSnapshots"]} ({ViewModel.Snapshots.Count})",
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 4, 0, 4)
            };

            var snapList = new StackPanel { Spacing = 6, Padding = new Thickness(4) };
            foreach (var s in ViewModel.Snapshots)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                row.Children.Add(new FontIcon { Glyph = "\uE7A6", FontSize = 12, Opacity = 0.6 });
                row.Children.Add(new TextBlock
                {
                    Text = $"{s.DisplayVersion ?? s.Label}  ·  {s.WrittenUtc.ToLocalTime():dd.MM.yyyy HH:mm}",
                    FontSize = 12,
                    Opacity = 0.8
                });
                snapList.Children.Add(row);
            }
            snapExpander.Content = snapList;
            panel.Children.Add(snapExpander);
        }

        // Remote GitHub release history
        if (ViewModel.HasRepoUrl)
        {
            var remoteExpander = new Expander
            {
                Header = new TextBlock
                {
                    Text = loc["DetailsRemoteReleases"],
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                },
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 4, 0, 4)
            };

            var remoteContainer = new StackPanel { Spacing = 8, Padding = new Thickness(4) };
            var btnFetch = new Button
            {
                Content = loc["DetailsFetchReleases"],
                FontSize = 11,
                Padding = new Thickness(10, 4, 10, 4)
            };

            var releasesList = new StackPanel { Spacing = 4 };
            btnFetch.Click += async (_, _) =>
            {
                btnFetch.IsEnabled = false;
                await ViewModel.FetchRemoteReleasesCommand.ExecuteAsync(null);
                releasesList.Children.Clear();
                foreach (var r in ViewModel.RemoteReleases)
                {
                    var dateStr = r.PublishedAt != default ? r.PublishedAt.ToLocalTime().ToString("dd.MM.yyyy") : "recent";
                    var rBlock = new TextBlock
                    {
                        Text = $"• {r.TagName} ({dateStr})",
                        FontSize = 12,
                        Opacity = 0.85
                    };
                    releasesList.Children.Add(rBlock);
                }
                btnFetch.Visibility = Visibility.Collapsed;
            };

            remoteContainer.Children.Add(btnFetch);
            remoteContainer.Children.Add(releasesList);
            remoteExpander.Content = remoteContainer;
            panel.Children.Add(remoteExpander);
        }

        // ── What's new (pending release notes) ─────────────────────
        if (ViewModel.UpdateAvailable)
        {
            panel.Children.Add(SectionHeader(loc["DetailsNotes"], "\uE70F"));

            var notes = ViewModel.FormattedPendingNotes;
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
                        TextWrapping = TextWrapping.Wrap
                    }
                    : new ScrollViewer
                    {
                        Content = new TextBlock
                        {
                            Text = notes,
                            FontSize = 12,
                            Opacity = 0.85,
                            TextWrapping = TextWrapping.Wrap
                        },
                        MaxHeight = 180,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
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
            IsOn = ViewModel.AutoApplyConsent
        };
        consent.Toggled += (_, _) => ViewModel.AutoApplyConsent = consent.IsOn;

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
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
        panel.Children.Add(consentContainer);

        // ── History (last entries for this app) ────────────────────
        if (ViewModel.History.Count > 0)
        {
            panel.Children.Add(SectionHeader(loc["DetailsHistory"], "\uE81C"));

            foreach (var h in ViewModel.History)
            {
                var line = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 6) };
                line.Children.Add(new TextBlock
                {
                    Text = $"{h.FromVersion} → {h.ToVersion}  ·  {h.AtUtc.ToLocalTime():dd.MM HH:mm}",
                    FontSize = 12,
                    Opacity = 0.8
                });
                line.Children.Add(new TextBlock
                {
                    Text = (h.Success ? "✓ " : "✗ ") + (h.Detail ?? (h.Success ? loc["Updated"] : loc["Error"])),
                    FontSize = 11,
                    Opacity = h.Success ? 0.6 : 0.85,
                    TextWrapping = TextWrapping.Wrap
                });
                panel.Children.Add(line);
            }
        }

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = 580,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
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
}
