namespace Alejandria.Models;

// Configuración de un área del modelo (SMT, BE o TLA).
// Cada campo guarda un estatus, Ya existente o quién lo hizo
public class DetalleConfiguracion
{
    public int ModeloId { get; set; }

    // SMT, BE, TLA o SMT-BE
    public string Area { get; set; } = string.Empty;

    // Base de datos iMES
    public string ImesModelsParents { get; set; } = string.Empty;
    public string ImesModelsMaster { get; set; } = string.Empty;
    public string ImesModelsSerialNumberSpecs { get; set; } = string.Empty;
    // Packing ya no se usa, se deja la columna por compatibilidad
    public string ImesModelsSerialNumberSpecsPacking { get; set; } = string.Empty;

    // Unity ya no se usa, se dejan las columnas por compatibilidad
    public string UnityModelsParents { get; set; } = string.Empty;
    public string UnityModelsMaster { get; set; } = string.Empty;
    public string UnityModelsSerialNumberSpecs { get; set; } = string.Empty;
    public string UnityModelsSerialNumberSpecsPacking { get; set; } = string.Empty;

    // Plataforma iMES
    public string WorkOrder { get; set; } = string.Empty;
    public string Flujo { get; set; } = string.Empty;
    public string Ruta { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string PartLineBomMaintenance { get; set; } = string.Empty;

    // Solo SMT-BE: el WO y la línea de BE (su ruta va en Flujo)
    public string WorkOrderBe { get; set; } = string.Empty;
    public string LineaBe { get; set; } = string.Empty;

    // Correcto o Pendiente a correcciones; si está pendiente, en Histórico se habilita Adjuntar PDF
    public string EstadoDocumento { get; set; } = "Correcto";

    // MODELID de MODELS_MASTER (Paso 2), lo usa el Paso 3
    public string ImesModelId { get; set; } = string.Empty;

    // Si tiene algo capturado
    public bool TieneDatos =>
        !string.IsNullOrWhiteSpace(ImesModelsParents) || !string.IsNullOrWhiteSpace(ImesModelsMaster) || !string.IsNullOrWhiteSpace(ImesModelsSerialNumberSpecs) || !string.IsNullOrWhiteSpace(ImesModelsSerialNumberSpecsPacking) ||
        !string.IsNullOrWhiteSpace(WorkOrder) || !string.IsNullOrWhiteSpace(Flujo) || !string.IsNullOrWhiteSpace(Ruta) || !string.IsNullOrWhiteSpace(Linea) || !string.IsNullOrWhiteSpace(PartLineBomMaintenance) ||
        !string.IsNullOrWhiteSpace(WorkOrderBe) || !string.IsNullOrWhiteSpace(LineaBe);
}
