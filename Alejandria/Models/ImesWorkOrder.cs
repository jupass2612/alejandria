using System.Text.Json.Serialization;

namespace Alejandria.Models;

// Work order de iMES; el área sale de LINE, no del nombre
public class ImesWorkOrder
{
    [JsonPropertyName("WORK_ORDER")]
    public string WorkOrder { get; set; } = string.Empty;

    [JsonPropertyName("IPN")]
    public string Ipn { get; set; } = string.Empty;

    // Línea física, de aquí sale el área
    [JsonPropertyName("LINE")]
    public string Line { get; set; } = string.Empty;

    [JsonPropertyName("lineId")]
    public string LineId { get; set; } = string.Empty;

    [JsonPropertyName("ROUTE_NAME")]
    public string RouteName { get; set; } = string.Empty;

    [JsonPropertyName("WO_STATUS")]
    public string WoStatus { get; set; } = string.Empty;

    [JsonPropertyName("WO_CREATE_DATE")]
    public string WoCreateDate { get; set; } = string.Empty;
}

// Respuesta de la API: success, code, msg, data
public class ImesApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("msg")]
    public string Msg { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();
}

// Error 400 cuando falta un parámetro
public class ImesValidationError
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("errors")]
    public Dictionary<string, List<string>>? Errors { get; set; }
}
