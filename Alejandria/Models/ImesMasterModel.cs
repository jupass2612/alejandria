namespace Alejandria.Models;

// Un renglón de IMES.MODELS_MASTER — el modelo en sí (ej. "Chargeport
// Controller"), ligado a un ImesParentModel por PARENTMODELID.
public class ImesMasterModel
{
    public int Id { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string ModelDesc { get; set; } = string.Empty;
    public int? ParentModelId { get; set; }
    public string ModelPartNumber { get; set; } = string.Empty;
    public string ModelQrPartNumber { get; set; } = string.Empty;
    public string ModelProductId { get; set; } = string.Empty;
    public int? PcbsPerPanel { get; set; }
    public string UiColor { get; set; } = string.Empty;
    public bool SimulateInterlock { get; set; }
    public bool ValidateInterlock { get; set; }
    public bool SerialNumberByMes { get; set; }
    public string SerialNumberCreationSp { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool ForceSecondarySerialNumber { get; set; }
    public string ModelPartNumber2 { get; set; } = string.Empty;
    public string FgPartNumber { get; set; } = string.Empty;
    public string HandlingLogic { get; set; } = string.Empty;
}
