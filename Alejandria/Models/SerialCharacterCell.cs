using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace Alejandria.Models;

// Una casilla del serial de ejemplo, para el armado visual de
// MODELS_SERIALNUMBERSPECS — clic secuencial, de izquierda a derecha.
public class SerialCharacterCell
{
    public char Character { get; init; }
    public string DisplayChar => Character.ToString();

    // Posición dentro del serial, empezando en 1 (así coincide directo
    // con STARTPOSITION de la tabla).
    public int Position { get; init; }

    // -1 = todavía no forma parte de ningún segmento confirmado.
    public int IndiceSegmento { get; init; } = -1;

    public bool YaAsignada => IndiceSegmento >= 0;

    public Brush Fondo { get; init; } = new SolidColorBrush(Colors.Transparent);
}
