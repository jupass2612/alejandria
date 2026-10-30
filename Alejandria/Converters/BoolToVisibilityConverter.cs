using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Alejandria.Converters;

// Convierte bool a Visibility. ConverterParameter="Invert" para invertir el resultado.
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var flag = value is bool b && b;

        if (parameter is string p && p.Equals("Invert", StringComparison.OrdinalIgnoreCase))
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
