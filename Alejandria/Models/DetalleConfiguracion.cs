namespace Alejandria.Models;

// Configuración de UNA área de un modelo (SMT, BE o TLA): base de datos
// iMES y plataforma iMES. Cada campo guarda un estatus, "No Aplica", o
// el nombre de quien lo completó.
public class DetalleConfiguracion
{
    public int ModeloId { get; set; }

    // SMT, BE o TLA — junto con ModeloId identifica esta configuración.
    public string Area { get; set; } = string.Empty;

    // Config. Base de Datos - iMES
    public string ImesModelsParents { get; set; } = string.Empty;
    public string ImesModelsMaster { get; set; } = string.Empty;
    public string ImesModelsSerialNumberSpecs { get; set; } = string.Empty;
    public string ImesModelsSerialNumberSpecsPacking { get; set; } = string.Empty;

    // Config. Base de Datos - Unity — ya no se usa en la interfaz (se
    // quitó ese apartado), pero se dejan las columnas en la base de
    // datos por compatibilidad con configuraciones ya guardadas.
    public string UnityModelsParents { get; set; } = string.Empty;
    public string UnityModelsMaster { get; set; } = string.Empty;
    public string UnityModelsSerialNumberSpecs { get; set; } = string.Empty;
    public string UnityModelsSerialNumberSpecsPacking { get; set; } = string.Empty;

    // Configuración Plataforma iMES
    public string WorkOrder { get; set; } = string.Empty;
    public string Flujo { get; set; } = string.Empty;
    public string Ruta { get; set; } = string.Empty;
    public string Linea { get; set; } = string.Empty;
    public string PartLineBomMaintenance { get; set; } = string.Empty;

    // Estado del PDF de este área — "Correcto" o "Pendiente a
    // correcciones". Si queda pendiente, el área no puede llegar a
    // Terminado y en Histórico se habilita "Adjuntar PDF".
    public string EstadoDocumento { get; set; } = "Correcto";

    // MODELID que quedó en MODELS_MASTER (Paso 2), para armar el INSERT del Paso 3
    public string ImesModelId { get; set; } = string.Empty;

    // True si al menos un campo tiene algo capturado.
    public bool TieneDatos =>
        !string.IsNullOrWhiteSpace(ImesModelsParents) || !string.IsNullOrWhiteSpace(ImesModelsMaster) || !string.IsNullOrWhiteSpace(ImesModelsSerialNumberSpecs) || !string.IsNullOrWhiteSpace(ImesModelsSerialNumberSpecsPacking) ||
        !string.IsNullOrWhiteSpace(WorkOrder) || !string.IsNullOrWhiteSpace(Flujo) || !string.IsNullOrWhiteSpace(Ruta) || !string.IsNullOrWhiteSpace(Linea) || !string.IsNullOrWhiteSpace(PartLineBomMaintenance);
}
