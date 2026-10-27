using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace AppWatch.Converters;

/// <summary>
/// Converts a tab index to Visibility: returns Visible when the value equals the parameter,
/// Collapsed otherwise. Parameter should be the target tab index (as string or int).
/// </summary>
public sealed class TabIndexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not int currentIndex) return Visibility.Collapsed;
        if (parameter is not int targetIndex && !int.TryParse(parameter?.ToString(), out targetIndex))
            return Visibility.Collapsed;
        
        return currentIndex == targetIndex ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}