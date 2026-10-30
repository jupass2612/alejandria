namespace Alejandria.Models;

// Para navegar a "Configurar en iMES" desde "Configurar modelo" — le
// dice para qué modelo y qué área es, así puede precargar los datos en
// vez de empezar de cero.
public class ConfigurarEnImesNavParams
{
    public required ModelRecord Modelo { get; init; }
    public required string Area { get; init; }
}
