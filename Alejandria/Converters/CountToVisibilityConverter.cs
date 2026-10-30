using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Alejandria.Converters;

// Convierte un conteo a Visibility: Visible si es 0, Collapsed si no.
// Para mostrar un mensaje de "no hay datos" sobre una lista vacía.
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var count = value is int intValue ? intValue : 0;
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
