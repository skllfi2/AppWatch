using AppWatch.Core.Domain;
using AppWatch.Core.Domain.Models;
using AppWatch.Infrastructure.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AppWatch.Views;

/// <summary>
/// B10 — pick an archived version to roll back to (pure C# dialog: XAML
/// ContentDialog files crash the XamlCompiler, see AddEditAppDialog).
/// </summary>
public sealed class RollbackVersionDialog : ContentDialog
{
    private readonly VersionStore _versions;
    private readonly TrackedApp _app;

    public VersionArchiveEntry? SelectedEntry { get; private set; }

    public RollbackVersionDialog(TrackedApp app)
    {
        _app = app;
        _versions = App.Resolve<VersionStore>();

        var loc = Localization.Loc.Instance;
        Title = string.Format(loc["RollbackDialogTitle"], app.Name);
        PrimaryButtonText = loc["RollbackDo"];
        SecondaryButtonText = loc["Cancel"];
        DefaultButton = ContentDialogButton.Primary;

        Content = BuildContent();
        MinWidth = 440;
    }

    private ScrollViewer BuildContent()
    {
        var loc = Localization.Loc.Instance;
        var entries = _versions.List(_app.Name);

        var panel = new StackPanel { Spacing = 10, Padding = new Thickness(0, 8, 0, 0) };

        if (entries.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = loc["RollbackEmpty"],
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7,
            });
            IsPrimaryButtonEnabled = false;
        }
        else
        {
            var group = new StackPanel { Spacing = 4 };
            foreach (var entry in entries)
            {
                var row = new RadioButton
                {
                    Content = $"{entry.Label}   —   {entry.WrittenUtc.ToLocalTime():dd.MM.yyyy HH:mm}",
                    Tag = entry,
                    IsChecked = ReferenceEquals(entry, entries[0]),
                    Margin = new Thickness(0, 2, 0, 2),
                };
                row.Checked += (_, _) => SelectedEntry = (VersionArchiveEntry)row.Tag;
                group.Children.Add(row);
            }
            SelectedEntry = entries[0];
            panel.Children.Add(group);
            panel.Children.Add(new TextBlock
            {
                Text = loc["RollbackHint"],
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.6,
                FontSize = 12,
            });
        }

        return new ScrollViewer
        {
            Content = panel,
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }
}