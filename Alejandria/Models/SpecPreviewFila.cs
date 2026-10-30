namespace Alejandria.Models;

// Una fila de la vista previa de MODELS_SERIALNUMBERSPECS
public class SpecPreviewFila
{
    public string ValueDescription { get; init; } = string.Empty;
    public int StartPosition { get; init; }
    public int ExpectedCharacterLen { get; init; }
    public bool IsDynamicValue { get; init; }

    // Lo que se va a guardar en FIXEDCHARACTERVALUE (vacío = NULL)
    public string FixedCharacterValue { get; init; } = string.Empty;

    public string StartTexto => StartPosition.ToString();
    public string LenTexto => ExpectedCharacterLen.ToString();
    public string DinamicoTexto => IsDynamicValue ? "1" : "0";
    public string ValorTexto => string.IsNullOrEmpty(FixedCharacterValue) ? "NULL" : FixedCharacterValue;
}
