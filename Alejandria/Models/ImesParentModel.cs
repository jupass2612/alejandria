namespace Alejandria.Models;

// Un renglón de IMES.MODELS_PARENTS — la "familia" a la que pertenece un
// modelo (ej. "Charge Port"). Varios modelos en MODELS_MASTER pueden
// compartir el mismo PARENTMODELID.
public class ImesParentModel
{
    public int Id { get; set; }
    public string ParentModelName { get; set; } = string.Empty;
    public string PartNumber { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public bool ValidateFlow { get; set; }
    public bool ValidateLastOpResult { get; set; }
    public bool FlowFwdValidation { get; set; }
    public bool IsActive { get; set; } = true;
}
