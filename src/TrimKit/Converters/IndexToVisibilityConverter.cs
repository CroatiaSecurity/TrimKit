using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TrimKit.Converters;

/// <summary>
/// Converts an integer index to Visibility. Shows (Visible) when the bound value equals the ConverterParameter.
/// Usage: Visibility="{Binding SelectedTabIndex, Converter={StaticResource IndexToVis}, ConverterParameter=0}"
/// </summary>
public class IndexToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index && parameter is string paramStr && int.TryParse(paramStr, out var target))
        {
            return index == target ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
