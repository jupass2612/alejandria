using System.Reflection;

namespace Alejandria.Services;

// Lee CHANGELOG.md para las notas de versión.
// Formato: "## X.Y.Z" y la lista abajo
public static class ChangelogService
{
    public static string VersionActualTexto =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "desconocida";

    public static string ObtenerChangelogCompleto()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        return File.Exists(ruta) ? File.ReadAllText(ruta) : "No se encontró el historial de cambios (CHANGELOG.md).";
    }

    // Las líneas de esa versión, null si no está
    public static string? ObtenerNotasDeVersion(string version)
    {
        var lineas = ObtenerChangelogCompleto().Split('\n');
        var notas = new List<string>();
        var enLaSeccion = false;

        foreach (var lineaCruda in lineas)
        {
            var linea = lineaCruda.TrimEnd('\r');

            if (linea.StartsWith("## "))
            {
                if (enLaSeccion)
                {
                    break;
                }

                enLaSeccion = linea[3..].Trim() == version;
                continue;
            }

            if (enLaSeccion && !string.IsNullOrWhiteSpace(linea))
            {
                notas.Add(linea.Trim());
            }
        }

        return notas.Count > 0 ? string.Join("\n", notas) : null;
    }
}
