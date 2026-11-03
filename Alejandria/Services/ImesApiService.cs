using System.Net.Http.Headers;
using System.Text.Json;
using Alejandria.Models;

namespace Alejandria.Services;

// Cliente de la API de iMES: work orders por número de parte y modelo por serial.
// Usa la sesión de Windows (no hay login); un solo HttpClient para toda la app
public class ImesApiService
{
    private static readonly HttpClient SharedHttp = new(new HttpClientHandler
    {
        UseDefaultCredentials = true
    })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Caché por número de parte para no repetir llamadas en la misma sesión
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTime Expira, List<ImesWorkOrder> Resultado)> CacheWorkOrders = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(5);

    private readonly Uri _baseUrl;
    private readonly string? _bearerToken;

    public ImesApiService(string baseUrl, string? bearerToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                "La URL de la API de iMES no está configurada (ImesApi:BaseUrl en appsettings.json).");
        }

        _baseUrl = uri;
        _bearerToken = bearerToken;
    }

    // Work orders del número de parte, uno por línea; filtrar con FiltrarPorArea
    public async Task<List<ImesWorkOrder>> BuscarWorkOrdersAsync(string numeroDeParte)
    {
        if (string.IsNullOrWhiteSpace(numeroDeParte))
        {
            return new List<ImesWorkOrder>();
        }

        if (CacheWorkOrders.TryGetValue(numeroDeParte, out var enCache) && enCache.Expira > DateTime.UtcNow)
        {
            return enCache.Resultado;
        }

        using var request = CrearRequest($"imes/getWorkOrderForNonLine?Ipn={Uri.EscapeDataString(numeroDeParte)}");
        using var response = await SharedHttp.SendAsync(request);
        var contenido = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(DescribirError(response.StatusCode, contenido));
        }

        var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var resultado = JsonSerializer.Deserialize<ImesApiResponse<ImesWorkOrder>>(contenido, opciones);

        if (resultado is null || !resultado.Success)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(resultado?.Msg) ? "iMES no devolvió información para ese número de parte." : resultado.Msg);
        }

        CacheWorkOrders[numeroDeParte] = (DateTime.UtcNow.Add(DuracionCache), resultado.Data);
        return resultado.Data;
    }

    // Para forzar una consulta nueva
    public static void LimpiarCache() => CacheWorkOrders.Clear();

    // Filtra por LINE (L5SMT, Backend_QRO_1), no por el nombre del WO
    public static List<ImesWorkOrder> FiltrarPorArea(IEnumerable<ImesWorkOrder> workOrders, string area)
    {
        var patrones = area switch
        {
            "SMT" => new[] { "SMT" },
            "BE" => new[] { "BACKEND", "BE" },
            "TLA" => new[] { "TLA", "FINAL" },
            _ => Array.Empty<string>()
        };

        if (patrones.Length == 0)
        {
            return new List<ImesWorkOrder>();
        }

        return workOrders
            .Where(wo => patrones.Any(p => wo.Line.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    // Modelo del serial; null si iMES no lo reconoce
    public async Task<ImesModeloPorSerial?> ObtenerModeloPorSerialAsync(string serialNumber)
    {
        var resultado = await ObtenerModeloPorSerialConPayloadAsync(serialNumber);
        return resultado.Modelo;
    }

    // Igual pero con todos los campos del payload
    public async Task<(ImesModeloPorSerial? Modelo, List<(string Campo, string Valor)> CamposCompletos)> ObtenerModeloPorSerialConPayloadAsync(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return (null, new List<(string, string)>());
        }

        using var request = CrearRequest($"imes/SMT/QRO/GetModelDataFromSn?serialNumber={Uri.EscapeDataString(serialNumber)}");
        using var response = await SharedHttp.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (null, new List<(string, string)>());
        }

        var contenido = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(DescribirError(response.StatusCode, contenido));
        }

        if (string.IsNullOrWhiteSpace(contenido) || contenido.Trim() is "null" or "{}")
        {
            return (null, new List<(string, string)>());
        }

        var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var modelo = JsonSerializer.Deserialize<ImesModeloPorSerial>(contenido, opciones);
        var campos = ExtraerCamposDelPayload(contenido);

        return modelo is null || string.IsNullOrWhiteSpace(modelo.ModelName) ? (null, campos) : (modelo, campos);
    }

    // JSON completo a lista de campo y valor
    private static List<(string Campo, string Valor)> ExtraerCamposDelPayload(string json)
    {
        var campos = new List<(string, string)>();
        try
        {
            using var documento = JsonDocument.Parse(json);
            foreach (var propiedad in documento.RootElement.EnumerateObject())
            {
                campos.Add((propiedad.Name, DescribirValorJson(propiedad.Value)));
            }
        }
        catch (JsonException)
        {
            // Si no se puede parsear, lista vacía
        }

        return campos;
    }

    private static string DescribirValorJson(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => "—",
        JsonValueKind.String => valor.GetString() ?? "—",
        JsonValueKind.True or JsonValueKind.False => valor.GetBoolean() ? "Sí" : "No",
        JsonValueKind.Array => string.Join(", ", valor.EnumerateArray().Select(DescribirValorJson)),
        JsonValueKind.Object => string.Join(", ", valor.EnumerateObject().Select(p => $"{p.Name}: {DescribirValorJson(p.Value)}")),
        _ => valor.ToString()
    };

    // URL y auth van en el request para que no se pisen dos llamadas
    private HttpRequestMessage CrearRequest(string rutaRelativa)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_baseUrl, rutaRelativa));

        if (!string.IsNullOrWhiteSpace(_bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);
        }

        return request;
    }

    private static string DescribirError(System.Net.HttpStatusCode status, string contenido)
    {
        try
        {
            var opciones = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var error = JsonSerializer.Deserialize<ImesValidationError>(contenido, opciones);
            if (error?.Errors is { Count: > 0 })
            {
                var detalle = string.Join("; ", error.Errors.SelectMany(kv => kv.Value.Select(msg => $"{kv.Key}: {msg}")));
                return $"iMES rechazó la consulta ({(int)status}): {detalle}";
            }
        }
        catch (JsonException)
        {
            // Otro formato, se usa el mensaje genérico
        }

        return $"iMES respondió con un error ({(int)status}). Verifica tu conexión de red/VPN a la empresa.";
    }
}
