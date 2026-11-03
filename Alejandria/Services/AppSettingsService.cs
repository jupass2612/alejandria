using System.Text.Json;

namespace Alejandria.Services;

// Lee o crea appsettings.json junto al .exe
public class AppSettingsService
{
    private readonly string _configPath;

    // Ruta de red de la base compartida
    public string DatabasePath { get; private set; } = string.Empty;

    // URL de la API de iMES; vacío deshabilita Buscar en iMES
    public string ImesApiBaseUrl { get; private set; } = string.Empty;

    // Token opcional, normalmente va con la sesión de Windows
    public string ImesApiBearerToken { get; private set; } = string.Empty;

    // Carpeta de red de actualizaciones de Velopack (lo que genera vpk pack)
    public string UpdatesFolder { get; private set; } = string.Empty;

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
            // Config vieja sin ImesApi
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
            // Config vieja sin Updates
            UpdatesFolder = @"\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\Alejandria\Actualizaciones";
            faltaAlgo = true;
        }

        // Config vieja con datos de Oracle, se limpia
        if (document.RootElement.TryGetProperty("ImesOracle", out _))
        {
            faltaAlgo = true;
        }

        if (faltaAlgo)
        {
            // Se completa y se guarda sin tocar DatabasePath
            GuardarConfigActual();
        }

        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            throw new InvalidOperationException(
                $"'DatabasePath' no está configurado en {_configPath}. Edita ese archivo y apunta a la ruta de red compartida.");
        }
    }

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
                }
            };

            var json = JsonSerializer.Serialize(actualizado, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception)
        {
            // Si no se pudo escribir, sigue con lo que hay en memoria
        }
    }

    private void CreateDefaultConfig()
    {
        var defaultConfig = new
        {
            // Base compartida, igual en todas las PCs
            DatabasePath = @"\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\DB\altas_modelos.db",
            ImesApi = new
            {
                BaseUrl = "https://sqromx-imesweb.ad.one-bcs.com/imes-api/",
                BearerToken = ""
            },
            Updates = new
            {
                Folder = @"\\sqromx-prfs01\Departments\IT\MES\Juan Pablo Solis\Alejandria\Actualizaciones"
            }
        };

        var json = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }
}
