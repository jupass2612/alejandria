using System.Text.Json;
using Alejandria.Models;

namespace Alejandria.Services;

// Lee (o crea) appsettings.json junto al ejecutable — ahí vive la ruta
// de red a la base de datos compartida.
public class AppSettingsService
{
    private readonly string _configPath;

    // Entorno que ya no existe (se quitó en 1.17.0)
    private const string EntornoDocker = "Docker";

    // Ruta de red de la base compartida
    public string DatabasePath { get; private set; } = string.Empty;

    // URL base de la API de iMES. Vacío = "Buscar en iMES" se deshabilita.
    public string ImesApiBaseUrl { get; private set; } = string.Empty;

    // Token Bearer opcional. Por defecto se usa autenticación de Windows;
    // esto es un respaldo por si algún día dan un token fijo.
    public string ImesApiBearerToken { get; private set; } = string.Empty;

    // Carpeta de red con el canal de actualizaciones de Velopack (ahí vive
    // lo que genera "vpk pack" — ver Installer/LEEME-VELOPACK.md).
    // Vacío = "Buscar actualización" avisa que no está configurado.
    public string UpdatesFolder { get; private set; } = string.Empty;

    // Entornos de Oracle de iMES: Productivo y Pruebas
    public string ImesOracleEntornoActivo { get; private set; } = "Pruebas";
    public Dictionary<string, ImesOracleEnvironment> ImesOracleEntornos { get; private set; } = new();

    public ImesOracleEnvironment? ImesOracleEntornoSeleccionado =>
        ImesOracleEntornos.TryGetValue(ImesOracleEntornoActivo, out var entorno) ? entorno : null;

    public AppSettingsService()
    {
        _configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    }

    public void Load()
    {
        if (!File.Exists(_configPath))
        {
            CreateDefaultConfig();
        }

        var json = File.ReadAllText(_configPath);
        using var document = JsonDocument.Parse(json);
        DatabasePath = document.RootElement.TryGetProperty("DatabasePath", out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;

        var faltaAlgo = false;

        // Config vieja con la carpeta de pruebas local, se limpia
        if (document.RootElement.TryGetProperty("DatabasePathPruebaLocal", out _))
        {
            faltaAlgo = true;
        }

        if (document.RootElement.TryGetProperty("ImesApi", out var imesApi))
        {
            ImesApiBaseUrl = imesApi.TryGetProperty("BaseUrl", out var baseUrl) ? baseUrl.GetString() ?? string.Empty : string.Empty;
            ImesApiBearerToken = imesApi.TryGetProperty("BearerToken", out var token) ? token.GetString() ?? string.Empty : string.Empty;
        }
        else
        {
            // Archivo de antes de que existiera la integración con iMES.
            ImesApiBaseUrl = "https://sqromx-imesweb.ad.one-bcs.com/imes-api/";
            ImesApiBearerToken = string.Empty;
            faltaAlgo = true;
        }

        if (document.RootElement.TryGetProperty("Updates", out var updates))
        {
            UpdatesFolder = updates.TryGetProperty("Folder", out var folder) ? folder.GetString() ?? string.Empty : string.Empty;
        }
        else
        {
            // Archivo de antes de que existiera "Buscar actualización".
            UpdatesFolder = @"\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\Alejandria\Actualizaciones";
            faltaAlgo = true;
        }

        if (document.RootElement.TryGetProperty("ImesOracle", out var imesOracle))
        {
            ImesOracleEntornoActivo = imesOracle.TryGetProperty("EntornoActivo", out var activo)
                ? activo.GetString() ?? "Pruebas"
                : "Pruebas";

            ImesOracleEntornos = new Dictionary<string, ImesOracleEnvironment>();
            if (imesOracle.TryGetProperty("Entornos", out var entornos))
            {
                foreach (var entorno in entornos.EnumerateObject())
                {
                    // Docker ya no se usa, se descarta
                    if (string.Equals(entorno.Name, EntornoDocker, StringComparison.OrdinalIgnoreCase))
                    {
                        faltaAlgo = true;
                        continue;
                    }

                    ImesOracleEntornos[entorno.Name] = new ImesOracleEnvironment
                    {
                        Host = entorno.Value.TryGetProperty("Host", out var host) ? host.GetString() ?? string.Empty : string.Empty,
                        Puerto = entorno.Value.TryGetProperty("Puerto", out var puerto) ? puerto.GetInt32() : 1521,
                        ServicioOSid = entorno.Value.TryGetProperty("ServicioOSid", out var servicio) ? servicio.GetString() ?? string.Empty : string.Empty,
                        Usuario = entorno.Value.TryGetProperty("Usuario", out var usuario) ? usuario.GetString() ?? string.Empty : string.Empty,
                        Password = entorno.Value.TryGetProperty("Password", out var password) ? password.GetString() ?? string.Empty : string.Empty
                    };
                }
            }

            // Si estaba en Docker se pasa a Pruebas
            if (!ImesOracleEntornos.ContainsKey(ImesOracleEntornoActivo))
            {
                ImesOracleEntornoActivo = "Pruebas";
                faltaAlgo = true;
            }

            foreach (var (nombre, entorno) in CrearEntornosPorDefecto())
            {
                if (ImesOracleEntornos.TryAdd(nombre, entorno))
                {
                    faltaAlgo = true;
                }
            }
        }
        else
        {
            // Config vieja sin entornos de Oracle, van en blanco para llenarse a mano
            ImesOracleEntornoActivo = "Pruebas";
            ImesOracleEntornos = CrearEntornosPorDefecto();
            faltaAlgo = true;
        }

        if (faltaAlgo)
        {
            // Se completa con los valores por defecto y se guarda, sin
            // tocar el DatabasePath que ya tenía la computadora.
            GuardarConfigActual();
        }

        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            throw new InvalidOperationException(
                $"'DatabasePath' no está configurado en {_configPath}. Edita ese archivo y apunta a la ruta de red compartida.");
        }
    }

    // Cambia el entorno activo desde Ajustes y lo guarda
    public void GuardarEntornoImesOracleActivo(string entorno)
    {
        ImesOracleEntornoActivo = entorno;
        GuardarConfigActual();
    }

    private static Dictionary<string, ImesOracleEnvironment> CrearEntornosPorDefecto() => new()
    {
        ["Productivo"] = new ImesOracleEnvironment(),
        ["Pruebas"] = new ImesOracleEnvironment()
    };

    private void GuardarConfigActual()
    {
        try
        {
            var actualizado = new
            {
                DatabasePath,
                ImesApi = new
                {
                    BaseUrl = ImesApiBaseUrl,
                    BearerToken = ImesApiBearerToken
                },
                Updates = new
                {
                    Folder = UpdatesFolder
                },
                ImesOracle = new
                {
                    EntornoActivo = ImesOracleEntornoActivo,
                    Entornos = ImesOracleEntornos
                }
            };

            var json = JsonSerializer.Serialize(actualizado, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception)
        {
            // No es grave si no se pudo escribir (ej. carpeta de solo
            // lectura) — la app sigue funcionando con el valor por defecto
            // en memoria, solo no queda guardado para la próxima vez.
        }
    }

    private void CreateDefaultConfig()
    {
        var defaultConfig = new
        {
            // Ruta de red compartida donde vive la base de datos de Altas de
            // Modelos. Debe ser IDÉNTICA en cada computadora.
            DatabasePath = @"\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\DB\altas_modelos.db",
            ImesApi = new
            {
                BaseUrl = "https://sqromx-imesweb.ad.one-bcs.com/imes-api/",
                BearerToken = ""
            },
            Updates = new
            {
                Folder = @"\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\Alejandria\Actualizaciones"
            },
            ImesOracle = new
            {
                EntornoActivo = "Pruebas",
                Entornos = CrearEntornosPorDefecto()
            }
        };

        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }
}
