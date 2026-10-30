using System.Collections.ObjectModel;

namespace AlejandriaCliente.Models;

// "Cambio de modelo": cuando cambia el número de parte de un modelo que
// ya existe — trae Línea/Flujo/Interfaces MES (porque la ruta puede
// cambiar con el nuevo número de parte), pero no PCB/SAP Information ni
// la parte más completa de Packout, porque eso ya quedó definido antes.
public class SolicitudCambioFlujo
{
    // 1. General Information
    public DateTimeOffset FechaSolicitud { get; set; } = DateTimeOffset.Now;
    public string NombreSolicitante { get; set; } = string.Empty;
    public string CorreoSolicitante { get; set; } = string.Empty;
    public DateTimeOffset? FechaEsperada { get; set; }

    // 2. Model Information — PCB y FG van separados, no como lista.
    public string NombreModelo { get; set; } = string.Empty;
    public string PcbModelSapPartNumber { get; set; } = string.Empty;
    public string FgModelSapPartNumber { get; set; } = string.Empty;
    public string BcsPartNumber { get; set; } = string.Empty;
    public string ClientPartNumber { get; set; } = string.Empty;
    public string PcbLaserSerialNumber { get; set; } = string.Empty;
    public string FgLaserSerialNumber { get; set; } = string.Empty;

    // 3. Información de línea
    public ObservableCollection<LineaInfo> Lineas { get; } = new() { new LineaInfo() };

    // 4 y 5. Diagrama de flujo (imagen adjunta por el usuario) + Interfaces MES
    public string? RutaImagenDiagramaFlujo { get; set; }
    public ObservableCollection<OperacionInfo> Operaciones { get; } = new() { new OperacionInfo() };

    // 6. Packout Setup Data (versión simple — sin nueva estación ni QR de producto)
    public string PackoutTipo { get; set; } = "FG";
    public bool ValidarFlujoAntesDePack { get; set; } = true;
    public string UltimaOperacionAntesDePackout { get; set; } = string.Empty;
    public string BoxLabelQr { get; set; } = "N/A";

    // 7. Aprobaciones
    public ObservableCollection<AprobacionEntry> Aprobaciones { get; } = new()
    {
        new AprobacionEntry { Rol = "Project Manager" },
        new AprobacionEntry { Rol = "Engineering" },
        new AprobacionEntry { Rol = "Quality" },
        new AprobacionEntry { Rol = "Operations" },
        new AprobacionEntry { Rol = "Materials & Production Planning" },
        new AprobacionEntry { Rol = "Finance" }
    };
}
