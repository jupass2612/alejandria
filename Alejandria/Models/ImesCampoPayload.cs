namespace Alejandria.Models;

// Un campo del payload de iMES al buscar un serial
public class ImesCampoPayload
{
    public string Campo { get; init; } = string.Empty;
    public string Valor { get; init; } = string.Empty;
}
