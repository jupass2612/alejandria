namespace Alejandria.Services;

// Registro de errores de la app en archivos de texto locales (por
// computadora, no se comparte por red) — uno por día, en
// %LOCALAPPDATA%\Alejandria\logs\. Se guardan los últimos 30 días y los
// más viejos se borran solos.
public static class LogService
{
    private static readonly string CarpetaLogs = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alejandria", "logs");

    private static readonly object Candado = new();

    // Las excepciones de .NET (IOException, UnauthorizedAccessException...)
    // suelen traer la ruta completa metida en el mensaje — como puede ser
    // una ruta de red con nombres de carpetas personales, se tapa antes de
    // guardarla. El tipo de excepción y el resto del mensaje se quedan
    // igual de útiles para diagnosticar; solo la ruta en sí desaparece.
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
            // Si ni la carpeta se puede crear, LogError/LogInfo lo intentan
            // de nuevo y fallan en silencio — no debe tronar el arranque.
        }
    }

    public static string CarpetaLogsRuta => CarpetaLogs;

    private static string RutaArchivoDeHoy => Path.Combine(CarpetaLogs, $"{DateTime.Now:yyyy-MM-dd}.log");

    // Tapa cualquier ruta de red en un texto — para usarse también en los
    // mensajes de error que se muestran en pantalla, no solo en el
    // registro, ya que las excepciones de .NET a veces meten la ruta
    // completa en su Message.
    public static string Redactar(string texto) => RutaDeRedRegex.Replace(texto, "[ruta de red]");

    // "Dónde pasó" (nombre del método/pantalla) + la excepción completa,
    // con su stack trace, para poder ubicar el problema después.
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
            // Si ni el registro de errores puede escribir, se deja pasar —
            // no debe tronar la app por esto.
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

    // Los últimos N días de logs juntos, más recientes primero — para
    // mostrarlos en el botón "Ver registro de errores" de Ajustes.
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
