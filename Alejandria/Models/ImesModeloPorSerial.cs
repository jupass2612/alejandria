using System.Text.Json.Serialization;

namespace Alejandria.Models;

// Lo que regresa iMES al buscar un serial (GetModelDataFromSn) — a qué
// modelo pertenece. Solo se mapean los campos que se muestran; el JSON
// real trae más.
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
