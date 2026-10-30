using System.Text.Json.Serialization;

namespace Alejandria.Models;

// Un work order de iMES. Un número de parte puede tener varios (uno por
// línea física), por eso el área se determina por Line, no por el
// nombre del work order. Solo se mapean los campos que se usan.
public class ImesWorkOrder
{
    [JsonPropertyName("WORK_ORDER")]
    public string WorkOrder { get; set; } = string.Empty;

    [JsonPropertyName("IPN")]
    public string Ipn { get; set; } = string.Empty;

    // La línea física — determina a qué área pertenece este work order.
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

// El sobre de respuesta de la API de iMES: {success, code, msg, data:[...]}.
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

// Error de validación de iMES cuando falta un parámetro (HTTP 400).
public class ImesValidationError
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("errors")]
    public Dictionary<string, List<string>>? Errors { get; set; }
}
