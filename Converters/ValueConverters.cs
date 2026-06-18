using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ProcessAnalyzerPro.Converters;

public class CpuLoadToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double cpu)
        {
            return cpu switch
            {
                >= 70 => new SolidColorBrush(Color.FromRgb(0xC0, 0x38, 0x58)),
                >= 40 => new SolidColorBrush(Color.FromRgb(0xB0, 0x70, 0x18)),
                _     => new SolidColorBrush(Color.FromRgb(0xA0, 0x50, 0x80))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0xC0, 0x78, 0x98));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public class NetworkToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count && count > 0)
            return new SolidColorBrush(Color.FromRgb(0x18, 0x98, 0xA8));
        return new SolidColorBrush(Color.FromRgb(0x88, 0x80, 0xA0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
