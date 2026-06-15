using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TrimKit.Models;

namespace TrimKit.Converters;

public class LogLevelToBrushConverter : IValueConverter
{
    // Chrome-dark theme colors matching GIDE/Ceprkac
    private static readonly SolidColorBrush InfoBrush = new(System.Windows.Media.Color.FromRgb(0xB4, 0xB8, 0xBE));
    private static readonly SolidColorBrush WarningBrush = new(System.Windows.Media.Color.FromRgb(0xFD, 0xD6, 0x63));
    private static readonly SolidColorBrush ErrorBrush = new(System.Windows.Media.Color.FromRgb(0xF2, 0x8B, 0x82));
    private static readonly SolidColorBrush SuccessBrush = new(System.Windows.Media.Color.FromRgb(0x81, 0xC9, 0x95));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => InfoBrush,
                LogLevel.Warning => WarningBrush,
                LogLevel.Error => ErrorBrush,
                LogLevel.Success => SuccessBrush,
                _ => InfoBrush
            };
        }
        return InfoBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
