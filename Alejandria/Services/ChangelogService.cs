using System.Reflection;

namespace Alejandria.Services;

// Lee CHANGELOG.md (junto al .exe) para el botón "Ver notas de versión" en
// Ajustes y para el "qué hay de nuevo" que sale al abrir después de una
// actualización. El formato esperado es un encabezado "## X.Y.Z" por
// versión, con los cambios de esa versión como lista debajo.
public static class ChangelogService
{
    public static string VersionActualTexto =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "desconocida";

    public static string ObtenerChangelogCompleto()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        return File.Exists(ruta) ? File.ReadAllText(ruta) : "No se encontró el historial de cambios (CHANGELOG.md).";
    }

    // Solo las líneas bajo el encabezado "## {version}" — null si esa
    // versión no aparece en el archivo.
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
