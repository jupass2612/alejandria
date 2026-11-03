namespace Alejandria.Models;

// Entrada del menú lateral
public class NavItem
{
    // Para saber a qué página va
    public string Tag { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    // Glyph de Segoe Fluent Icons
    public string Icon { get; set; } = string.Empty;

    // Página destino
    public Type? PageType { get; set; }
}
