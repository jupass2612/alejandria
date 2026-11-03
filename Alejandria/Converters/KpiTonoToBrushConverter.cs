using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Alejandria.Converters;

// Tono del KPI a color fijo; nunca regresa UnsetValue porque x:Bind truena
public sealed class KpiTonoToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var tono = value as string ?? string.Empty;

        var hex = tono switch
        {
            "Success" => "#22C55E",
            "Warning" => "#F59E0B",
            "Pendiente" => "#EAB308",
            "Danger" => "#EF4444",
            _ => "#64748B" // No debería pasar
        };

        var (r, g, b) = ParseRgb(hex);
        return new SolidColorBrush(Color.FromArgb(255, r, g, b));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();

    private static (byte R, byte G, byte B) ParseRgb(string hex)
    {
        hex = hex.TrimStart('#');
        var r = System.Convert.ToByte(hex.Substring(0, 2), 16);
        var g = System.Convert.ToByte(hex.Substring(2, 2), 16);
        var b = System.Convert.ToByte(hex.Substring(4, 2), 16);
        return (r, g, b);
    }
}
