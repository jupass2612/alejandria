using Alejandria.Services;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Alejandria.Converters;

// Estatus a color de badge
public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var status = value as string ?? string.Empty;
        var (r, g, b) = StatusColorHelper.GetRgb(status);
        return new SolidColorBrush(Color.FromArgb(255, r, g, b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
