namespace Alejandria.Services;

// Log de errores local, uno por día en %LOCALAPPDATA%\Alejandria\logs, 30 días
public static class LogService
{
    private static readonly string CarpetaLogs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alejandria", "logs");

    private static readonly object Candado = new();

    // Tapa rutas de red que .NET mete en los mensajes
    private static readonly System.Text.RegularExpressions.Regex RutaDeRedRegex = new(
        @"\\\\[^'""\r\n]+",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    static LogService()
    {
        try
        {
            Directory.CreateDirectory(CarpetaLogs);
        }
        catch (Exception)
        {
            // Si no se puede crear la carpeta, el log falla en silencio
        }
    }

    public static string CarpetaLogsRuta => CarpetaLogs;

    private static string RutaArchivoDeHoy => Path.Combine(CarpetaLogs, $"{DateTime.Now:yyyy-MM-dd}.log");

    // Tapa rutas de red también en los mensajes de pantalla
    public static string Redactar(string texto) => RutaDeRedRegex.Replace(texto, "[ruta de red]");

    // Contexto + excepción completa con stack trace
    public static void LogError(string contexto, Exception ex)
    {
        var mensaje = $"{contexto}\n{ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}";
        Escribir("ERROR", Redactar(mensaje));
    }

    public static void LogInfo(string mensaje)
    {
        Escribir("INFO", Redactar(mensaje));
    }

    private static void Escribir(string nivel, string mensaje)
    {
        try
        {
            lock (Candado)
            {
                var linea = $"[{DateTime.Now:HH:mm:ss}] [{nivel}] {mensaje}";
                File.AppendAllText(RutaArchivoDeHoy, linea + Environment.NewLine + Environment.NewLine);
                LimpiarLogsViejos();
            }
        }
        catch (Exception)
        {
            // Si el log no puede escribir, se deja pasar
        }
    }

    private static void LimpiarLogsViejos()
    {
        var archivosViejos = Directory.GetFiles(CarpetaLogs, "*.log")
            .OrderByDescending(f => f)
            .Skip(30);

        foreach (var archivo in archivosViejos)
        {
            File.Delete(archivo);
        }
    }

    // Últimos días de log, más recientes primero
    public static string LeerRecientes(int dias = 5)
    {
        if (!Directory.Exists(CarpetaLogs))
        {
            return "Sin errores registrados.";
        }

        var archivos = Directory.GetFiles(CarpetaLogs, "*.log")
            .OrderByDescending(f => f)
            .Take(dias)
            .ToList();

        if (archivos.Count == 0)
        {
            return "Sin errores registrados.";
        }

        var bloques = archivos.Select(archivo =>
        {
            var fecha = Path.GetFileNameWithoutExtension(archivo);
            var contenido = File.ReadAllText(archivo).Trim();
            return $"=== {fecha} ===\n{contenido}";
        });

        return string.Join("\n\n", bloques);
    }
}
