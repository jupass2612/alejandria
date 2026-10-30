using System.Collections.ObjectModel;

namespace AlejandriaCliente.Models;

// Un campo con su valor actual y el nuevo valor propuesto — el patrón que
// usa toda la solicitud de "Cambio de flujo".
public class CampoActualNuevo
{
    public string Etiqueta { get; init; } = string.Empty;
    public string Actual { get; set; } = string.Empty;
    public string Nuevo { get; set; } = string.Empty;
}

// Una fila de "PCB / SAP Information" para Cambio de flujo — con
// ACTUAL/NUEVO en cada uno de los 3 sub-campos.
public class PcbSapAreaCambio
{
    public string Area { get; init; } = string.Empty;
    public bool TieneReportSide { get; init; } = true;
    public string SapPartNumberReportActual { get; set; } = "N/A";
    public string SapPartNumberReportNuevo { get; set; } = "N/A";
    public string SapReportSideActual { get; set; } = "N/A";
    public string SapReportSideNuevo { get; set; } = "N/A";
    public string SapReportPointActual { get; set; } = "N/A";
    public string SapReportPointNuevo { get; set; } = "N/A";
}

// "Cambio de modelo": modificar algo ya registrado (a dónde reporta, cómo
// empaqueta) en un modelo existente — no se toca la línea/operaciones,
// por eso no trae esas secciones.
public class SolicitudCambioModelo
{
    // 1. General Information
    public DateTimeOffset FechaSolicitud { get; set; } = DateTimeOffset.Now;
    public string NombreSolicitante { get; set; } = string.Empty;
    public string CorreoSolicitante { get; set; } = string.Empty;
    public DateTimeOffset? FechaEsperada { get; set; }

    // 2. Model Information (Actual / Nuevo)
    public CampoActualNuevo ModelName { get; } = new() { Etiqueta = "Model Name" };
    public CampoActualNuevo ModelSapPartNumber { get; } = new() { Etiqueta = "Model SAP Part Number" };
    public CampoActualNuevo BcsPartNumber { get; } = new() { Etiqueta = "BCS Part Number" };
    public CampoActualNuevo ClientPartNumber { get; } = new() { Etiqueta = "Client Part Number" };
    public CampoActualNuevo LaserSerialNumber { get; } = new() { Etiqueta = "Laser Serial Number real example" };

    // 3. PCB / SAP Information (Actual / Nuevo)
    public CampoActualNuevo PcbsPerPanel { get; } = new() { Etiqueta = "PCBs per Panel" };
    public ObservableCollection<PcbSapAreaCambio> AreasPcbSap { get; } = new()
    {
        new PcbSapAreaCambio { Area = "SMT" },
        new PcbSapAreaCambio { Area = "BACKEND" },
        new PcbSapAreaCambio { Area = "TLA" },
        new PcbSapAreaCambio { Area = "PACKOUT", TieneReportSide = false }
    };

    // 4. Packout Setup Data (Actual usado / Nuevo a agregar)
    public CampoActualNuevo PackoutTipo { get; } = new() { Etiqueta = "Finish Good or PCB Packout?" };
    public CampoActualNuevo ValidarFlujoAntesDePack { get; } = new() { Etiqueta = "Validate flow before Pack?" };
    public CampoActualNuevo UltimaOperacionAntesDePackout { get; } = new() { Etiqueta = "Last operation before Packout" };
    public CampoActualNuevo NuevaEstacionPackout { get; } = new() { Etiqueta = "New Packout Station?" };
    public CampoActualNuevo BoxLabelQr { get; } = new() { Etiqueta = "Box Label QR code" };
    public CampoActualNuevo ProductLabelQr { get; } = new() { Etiqueta = "Product Label QR code" };

    // 5. Aprobaciones
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
