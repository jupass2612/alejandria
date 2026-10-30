using System.Net.Http.Headers;
using System.Text.Json;
using Alejandria.Models;

namespace Alejandria.Services;

// Cliente de la API de iMES (https://sqromx-imesweb.ad.one-bcs.com/imes-api/).
// Por ahora solo trae work orders por número de parte (BuscarWorkOrdersAsync)
// y busca modelo por serial (ObtenerModeloPorSerialAsync).
//
// Usa autenticación de Windows (UseDefaultCredentials) porque no hay un
// /login con usuario/contraseña que funcione — es la misma sesión con la
// que ya se entra a iMES sin escribir contraseña. Si algún día dan un
// token fijo, se puede poner en ImesApi:BearerToken en appsettings.json.
//
// El HttpClient (SharedHttp) es uno solo para toda la app, no uno nuevo
// por llamada — crear/destruir un HttpClient en cada clic es lento y
// puede agotar sockets.
public class ImesApiService
{
    private static readonly HttpClient SharedHttp = new(new HttpClientHandler
    {
        UseDefaultCredentials = true
    })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Caché en memoria de resultados por número de parte — para no volver
    // a llamar a la API si se entra y sale de la misma área varias veces
    // en la misma sesión (ej. por la búsqueda automática al cambiar de área).
    // Se comparte entre instancias porque se crea una ImesApiService nueva
    // en cada búsqueda.
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

    // Work orders de un número de parte. Puede haber varios (uno por
    // línea física). Usa FiltrarPorArea para quedarte solo con los de un área.
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

    // Por si alguien necesita forzar una consulta fresca (ej. un botón de
    // "volver a intentar" tras un cambio reciente en iMES).
    public static void LimpiarCache() => CacheWorkOrders.Clear();

    // Filtra por área usando el campo LINE (ej. "L5SMT", "Backend_QRO_1"),
    // no el nombre del work order, porque ese estándar va a cambiar.
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

    // Busca a qué modelo pertenece un serial, para corroborar
    // MODELS_SERIALNUMBERSPECS y su Packing. Null si no lo reconoce
    // (404 o cuerpo vacío) o si iMES manda un objeto vacío.
    public async Task<ImesModeloPorSerial?> ObtenerModeloPorSerialAsync(string serialNumber)
    {
        var resultado = await ObtenerModeloPorSerialConPayloadAsync(serialNumber);
        return resultado.Modelo;
    }

    // Igual que ObtenerModeloPorSerialAsync, pero además regresa TODOS los
    // campos que trae la respuesta de iMES (no solo los 4 que se mapean),
    // para poder mostrar el payload completo en pantalla.
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

    // Convierte el JSON completo de la respuesta a una lista plana de
    // (campo, valor) para mostrarla — sin quedarse solo con los 4 campos
    // que ya se usan para el resumen.
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
            // Si por alguna razón no se puede volver a parsear, se deja
            // la lista vacía — el resumen normal sigue funcionando.
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

    // La URL y el header de auth van en el request, no en el HttpClient
    // compartido — si se pusieran ahí, dos llamadas al mismo tiempo se
    // podrían pisar entre sí.
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
            // Cuerpo en un formato distinto — se usa el mensaje genérico de abajo.
        }

        return $"iMES respondió con un error ({(int)status}). Verifica tu conexión de red/VPN a la empresa.";
    }
}
