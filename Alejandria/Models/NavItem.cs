namespace Alejandria.Models;

// Una entrada del panel de navegación.
public class NavItem
{
    // Tag único para saber a qué página navegar.
    public string Tag { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    // Glyph de Segoe Fluent Icons.
    public string Icon { get; set; } = string.Empty;

    // Página a la que navega este item.
    public Type? PageType { get; set; }
}
