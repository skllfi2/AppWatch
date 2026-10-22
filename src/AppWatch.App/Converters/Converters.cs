using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using AppWatch.ViewModels;
using AppWatch.Core.Domain.Models;

namespace AppWatch.Converters;

// Ported from ZuiV2 Converters.cs (our own code).

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is bool b ? !b : value;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}

public sealed class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is string s && !string.IsNullOrEmpty(s);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count) return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return value is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>A1: non-empty string → Visible (drives the pin/skip rule badge).</summary>
public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is string s && s.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class InverseCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count) return count > 0 ? Visibility.Collapsed : Visibility.Visible;
        if (value is System.Collections.IList list) return list.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int count) return count > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (value is System.Collections.IList list) return list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

public sealed class StringToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && !string.IsNullOrEmpty(hex))
        {
            try
            {
                var color = Microsoft.UI.ColorHelper.FromArgb(255,
                    byte.Parse(hex[1..3], System.Globalization.NumberStyles.HexNumber),
                    byte.Parse(hex[3..5], System.Globalization.NumberStyles.HexNumber),
                    byte.Parse(hex[5..7], System.Globalization.NumberStyles.HexNumber));
                return new SolidColorBrush(color);
            }
            catch { }
        }
        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}

/// <summary>
/// UpdateStage → semantic brush (uses DesignSystem colors via x:Bind in XAML).
/// Since converters can't access StaticResource directly, the VM exposes StatusBrush directly.
/// This converter is kept for completeness (not used by AppsPage currently).
/// </summary>
public sealed class StageToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not UpdateStage stage) return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        return stage switch
        {
            UpdateStage.Done => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x2E, 0xA0, 0x43)),
            UpdateStage.Error => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0xD8, 0x3B, 0x01)),
            UpdateStage.Replacing => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0xFF, 0xB9, 0x00)),
            UpdateStage.Checking or UpdateStage.Downloading or UpdateStage.Restarting
                => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x00, 0x78, 0xD4)),
            _ => new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 0x61, 0x61, 0x61)),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// UpdateStage → Visibility for progress bar (visible during Downloading/Replacing).
/// </summary>
public sealed class StageToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not UpdateStage stage) return Visibility.Collapsed;
        return stage is UpdateStage.Downloading or UpdateStage.Replacing
            ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// First character of a string → brush from 8-color monogram palette.
/// </summary>
public sealed class FirstCharToBrushConverter : IValueConverter
{
    private static readonly uint[] Palette =
    [
        0xFF60CDFF, 0xFF4CAF50, 0xFF9C27B0, 0xFFFB5E5E,
        0xFFFFB900, 0xFF616161, 0xFF0078D4, 0xFF9E9E9E,
    ];

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string s || string.IsNullOrEmpty(s))
            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        var hash = Math.Abs(string.GetHashCode(s.AsSpan(0, 1), StringComparison.OrdinalIgnoreCase));
        var hex = Palette[hash % Palette.Length];
        return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255,
            (byte)(hex >> 16), (byte)(hex >> 8), (byte)hex));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

// /// <summary>
// /// String URL → BitmapImage for Image.Source binding in x:Bind.
// /// Returns null when string is empty/null so Image shows nothing (transparent).
// /// </summary>
public sealed class StringToImageSourceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string url || string.IsNullOrWhiteSpace(url))
            return null!;
        try
        {
            return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(url));
        }
        catch { return null!; }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
