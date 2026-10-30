namespace Alejandria.Models;

// Un campo del payload completo que regresó iMES al buscar un serial —
// para mostrarlo tal cual, campo por campo, en vez de solo el resumen.
public class ImesCampoPayload
{
    public string Campo { get; init; } = string.Empty;
    public string Valor { get; init; } = string.Empty;
}
