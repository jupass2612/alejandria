using System.Text.Json.Serialization;

namespace Alejandria.Models;

// Respuesta de GetModelDataFromSn, solo los campos que se usan
public class ImesModeloPorSerial
{
    [JsonPropertyName("ParentModelName")]
    public string ParentModelName { get; set; } = string.Empty;

    [JsonPropertyName("ModelName")]
    public string ModelName { get; set; } = string.Empty;

    [JsonPropertyName("ModelPartNumber")]
    public string ModelPartNumber { get; set; } = string.Empty;

    [JsonPropertyName("ModelPartNumber2")]
    public string? ModelPartNumber2 { get; set; }
}
