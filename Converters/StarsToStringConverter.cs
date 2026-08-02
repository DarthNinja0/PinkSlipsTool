using System.Globalization;
using System.Windows.Data;

namespace PinkSlipsTool.Converters;

public class StarsToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int stars)
            return stars == 10 ? "⭐⭐⭐⭐⭐" : new string('⭐', Math.Min(stars, 10));
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
