using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PinkSlipsTool.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var b = value is bool bv && bv;
        var invert = parameter is string s && s == "invert";
        return (b ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
