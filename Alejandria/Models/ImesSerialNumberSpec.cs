namespace Alejandria.Models;

// Un renglón de IMES.MODELS_SERIALNUMBERSPECS — describe UNA parte del
// número de serie de un modelo (ej. "Revision", posición 13, 1
// caracter, fijo "B"). Un modelo normalmente tiene varios de estos,
// uno por cada segmento del serial.
public class ImesSerialNumberSpec
{
    public int Id { get; set; }
    public int ModelId { get; set; }
    public string ValueDescription { get; set; } = string.Empty;
    public int StartPosition { get; set; }
    public int ExpectedCharacterLen { get; set; }
    public bool IsDynamicValue { get; set; }
    public string FixedCharacterValue { get; set; } = string.Empty;
}
