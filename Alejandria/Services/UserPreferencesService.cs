using System.Text.Json;

namespace Alejandria.Services;

// Preferencias de esta PC en %LOCALAPPDATA%\Alejandria\preferences.json
public class UserPreferencesService
{
    private readonly string _path;

    // Default (Windows), Light o Dark
    public string Theme { get; private set; } = "Default";

    public bool NotificationsEnabled { get; private set; } = true;

    // Buscar en iMES solo al entrar a un área sin datos
    public bool BusquedaImesAutomatica { get; private set; } = true;

    // Pasar solo al siguiente paso en Configurar
    public bool AvanceAutomaticoPasos { get; private set; } = true;

    // Última versión que vio el qué hay de nuevo; vacío también lo muestra
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
            // Si el archivo está dañado, se queda con los valores por defecto
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

    // Aparte de Save porque lo pone la app, no el usuario
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
