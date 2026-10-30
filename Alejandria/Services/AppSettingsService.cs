using System.Text.Json;
using Alejandria.Models;

namespace Alejandria.Services;

// Lee (o crea) appsettings.json junto al ejecutable — ahí vive la ruta
// de red a la base de datos compartida.
public class AppSettingsService
{
    private readonly string _configPath;

    // Carpeta de pruebas local por defecto — se usa cuando el archivo de
    // configuración es viejo y no tiene esto capturado todavía.
    private const string RutaPruebaLocalPorDefecto = @"C:\Users\solisj\OneDrive - BCS AIS\Documentos\Alejandria Prueba\altas_modelos.db";

    // Ruta real (compartida en red) — la que usan Productivo y Pruebas.
    public string DatabasePathProductivo { get; private set; } = string.Empty;

    // Carpeta local de pruebas — se usa en vez de la de red cuando el
    // entorno de Oracle activo es "Docker", para no mezclar datos de
    // prueba con los reales (base de datos, Documentos, y Respaldos
    // viven todos junto a esta ruta).
    public string DatabasePathPruebaLocal { get; private set; } = string.Empty;

    // La ruta que usa el resto de la app — cambia sola según el entorno
    // de Oracle activo (ver Ajustes → Entorno de iMES).
    public string DatabasePath =>
        string.Equals(ImesOracleEntornoActivo, "Docker", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(DatabasePathPruebaLocal)
            ? DatabasePathPruebaLocal
            : DatabasePathProductivo;

    // URL base de la API de iMES. Vacío = "Buscar en iMES" se deshabilita.
    public string ImesApiBaseUrl { get; private set; } = string.Empty;

    // Token Bearer opcional. Por defecto se usa autenticación de Windows;
    // esto es un respaldo por si algún día dan un token fijo.
    public string ImesApiBearerToken { get; private set; } = string.Empty;

    // Carpeta de red con el canal de actualizaciones de Velopack (ahí vive
    // lo que genera "vpk pack" — ver Installer/LEEME-VELOPACK.md).
    // Vacío = "Buscar actualización" avisa que no está configurado.
    public string UpdatesFolder { get; private set; } = string.Empty;

    // Los 3 entornos de la base de datos Oracle de iMES (Productivo,
    // Pruebas, Docker) — para cuando Alejandría escriba la configuración
    // ahí directamente, en vez de solo llevar el seguimiento de que se
    // hizo a mano. Ver docker/README.md para el entorno "Docker".
    public string ImesOracleEntornoActivo { get; private set; } = "Docker";
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
        DatabasePathProductivo = document.RootElement.TryGetProperty("DatabasePath", out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;

        var faltaAlgo = false;

        if (document.RootElement.TryGetProperty("DatabasePathPruebaLocal", out var pruebaLocal))
        {
            DatabasePathPruebaLocal = pruebaLocal.GetString() ?? string.Empty;
        }
        else
        {
            // Archivo de antes de que existiera la carpeta de pruebas local.
            DatabasePathPruebaLocal = RutaPruebaLocalPorDefecto;
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
                ? activo.GetString() ?? "Docker"
                : "Docker";

            ImesOracleEntornos = new Dictionary<string, ImesOracleEnvironment>();
            if (imesOracle.TryGetProperty("Entornos", out var entornos))
            {
                foreach (var entorno in entornos.EnumerateObject())
                {
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
        }
        else
        {
            // Archivo de antes de que existiera la parametrización de
            // entornos de Oracle — se completa con los 3 entornos vacíos
            // (Docker con los valores del docker-compose.yml de ejemplo,
            // Productivo/Pruebas en blanco para que se llenen a mano).
            ImesOracleEntornoActivo = "Docker";
            ImesOracleEntornos = CrearEntornosPorDefecto();
            faltaAlgo = true;
        }

        if (faltaAlgo)
        {
            // Se completa con los valores por defecto y se guarda, sin
            // tocar el DatabasePath que ya tenía la computadora.
            GuardarConfigActual();
        }

        if (string.IsNullOrWhiteSpace(DatabasePathProductivo))
        {
            throw new InvalidOperationException(
                $"'DatabasePath' no está configurado en {_configPath}. Edita ese archivo y apunta a la ruta de red compartida.");
        }
    }

    // Cambia el entorno activo (Productivo/Pruebas/Docker) y lo guarda —
    // se llama desde el selector en Ajustes.
    public void GuardarEntornoImesOracleActivo(string entorno)
    {
        ImesOracleEntornoActivo = entorno;
        GuardarConfigActual();
    }

    private static Dictionary<string, ImesOracleEnvironment> CrearEntornosPorDefecto() => new()
    {
        ["Productivo"] = new ImesOracleEnvironment(),
        ["Pruebas"] = new ImesOracleEnvironment(),
        ["Docker"] = new ImesOracleEnvironment
        {
            Host = "localhost",
            Puerto = 1521,
            ServicioOSid = "XEPDB1",
            Usuario = "IMES",
            Password = "DevOnly_ChangeMe!"
        }
    };

    private void GuardarConfigActual()
    {
        try
        {
            var actualizado = new
            {
                DatabasePath = DatabasePathProductivo,
                DatabasePathPruebaLocal,
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

            // Carpeta local de pruebas — se usa en vez de la de red cuando el
            // entorno de Oracle activo es "Docker" (ver Ajustes).
            DatabasePathPruebaLocal = RutaPruebaLocalPorDefecto,
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
                EntornoActivo = "Docker",
                Entornos = CrearEntornosPorDefecto()
            }
        };

        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }
}
