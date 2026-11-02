using System.Text.Json;

namespace Alejandria.Services;

// Preferencias locales de esta computadora (tema, confirmaciones), no
// compartidas por red. Se guardan en %LOCALAPPDATA%\Alejandria\preferences.json.
public class UserPreferencesService
{
    private readonly string _path;

    // "Default" (sigue a Windows), "Light" o "Dark".
    public string Theme { get; private set; } = "Default";

    public bool NotificationsEnabled { get; private set; } = true;

    // Si "Buscar en iMES" se dispara solo al entrar a un área con Work
    // Order/Ruta/Línea vacíos, en vez de tener que darle clic cada vez.
    // Prendido por default; se puede apagar para quien tenga una
    // conexión lenta y prefiera decidir cuándo consultar.
    public bool BusquedaImesAutomatica { get; private set; } = true;

    // En Configurar modelo, pasa solo al siguiente paso cuando se completa uno
    public bool AvanceAutomaticoPasos { get; private set; } = true;

    // Última versión que ya vio el "qué hay de nuevo" al abrir — vacío si
    // nunca se ha registrado (primera instalación real, o la primera vez
    // que corre una versión con este seguimiento). En ambos casos se
    // muestran las notas de la versión actual una vez.
    public string LastSeenVersion { get; private set; } = string.Empty;

    public UserPreferencesService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Alejandria");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "preferences.json");
    }

    public void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_path);
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.TryGetProperty("Theme", out var themeElement))
            {
                Theme = themeElement.GetString() ?? "Default";
            }

            if (document.RootElement.TryGetProperty("NotificationsEnabled", out var notifElement))
            {
                NotificationsEnabled = notifElement.GetBoolean();
            }

            if (document.RootElement.TryGetProperty("BusquedaImesAutomatica", out var busquedaElement))
            {
                BusquedaImesAutomatica = busquedaElement.GetBoolean();
            }

            if (document.RootElement.TryGetProperty("AvanceAutomaticoPasos", out var avanceElement))
            {
                AvanceAutomaticoPasos = avanceElement.GetBoolean();
            }

            if (document.RootElement.TryGetProperty("LastSeenVersion", out var versionElement))
            {
                LastSeenVersion = versionElement.GetString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            // Archivo corrupto o ilegible: seguimos con los valores por defecto
            // en vez de tronar el arranque de la app por una preferencia.
            LogService.LogError("UserPreferencesService.Load", ex);
        }
    }

    public void Save(string theme, bool notificationsEnabled, bool busquedaImesAutomatica, bool avanceAutomaticoPasos)
    {
        Theme = theme;
        NotificationsEnabled = notificationsEnabled;
        BusquedaImesAutomatica = busquedaImesAutomatica;
        AvanceAutomaticoPasos = avanceAutomaticoPasos;
        GuardarArchivo();
    }

    // Se llama aparte de Save (tema/notificaciones) porque esto no lo
    // decide el usuario en Ajustes — lo actualiza la app sola al mostrar
    // (o decidir no mostrar) el "qué hay de nuevo".
    public void MarcarVersionVista(string version)
    {
        LastSeenVersion = version;
        GuardarArchivo();
    }

    private void GuardarArchivo()
    {
        var json = JsonSerializer.Serialize(
            new { Theme, NotificationsEnabled, BusquedaImesAutomatica, AvanceAutomaticoPasos, LastSeenVersion },
            new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(_path, json);
    }
}
