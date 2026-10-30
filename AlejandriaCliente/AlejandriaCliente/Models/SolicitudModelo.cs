using System.Collections.ObjectModel;

namespace AlejandriaCliente.Models;

// Un serial de ejemplo — puede llevar etiqueta (FG, PCBA, T&C, Clock
// spring) o quedar en blanco si el modelo solo tiene un serial.
public class SerialEntry
{
    public string Etiqueta { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
}

// Una fila de la tabla "Line Information".
public class LineaInfo
{
    public string Nombre { get; set; } = "N/A";
    public string PcbSide { get; set; } = "N/A";
    public string Eficiencia { get; set; } = "N/A";
    public string CycleTime { get; set; } = "N/A";
    public string FullRate { get; set; } = "N/A";
    public string ExpectedRate { get; set; } = "N/A";
}

// Una operación del flujo — junta lo que en la solicitud original son dos
// tablas separadas (el diagrama de flujo, y las interfaces MES por
// operación), ya que describen lo mismo: una fila por operación.
public class OperacionInfo
{
    public string Numero { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string LineaAsociada { get; set; } = string.Empty;
    public string MesInterface { get; set; } = string.Empty;
    public bool RequeridaParaInterlock { get; set; }
    public bool TieneInterlockFisico { get; set; }
}

// Una fila de "PCB / SAP Information" — una por área.
public class PcbSapArea
{
    public string Area { get; init; } = string.Empty;
    public bool TieneReportSide { get; init; } = true;
    public string SapPartNumberReport { get; set; } = "N/A";
    public string SapReportSide { get; set; } = "N/A";
    public string SapReportPoint { get; set; } = "N/A";
}

// Una fila de la tabla de aprobaciones — el rol es fijo, solo se captura el nombre.
public class AprobacionEntry
{
    public string Rol { get; init; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

// Todo lo que captura el formulario de "Alejandría Cliente" — las mismas
// secciones que "BCS Unity System: New model & part number request".
public class SolicitudModelo
{
    // 1. Información General
    public DateTimeOffset FechaSolicitud { get; set; } = DateTimeOffset.Now;
    public string NombreSolicitante { get; set; } = string.Empty;
    public string CorreoSolicitante { get; set; } = string.Empty;
    public DateTimeOffset? FechaEsperada { get; set; }

    // 2. Información del Modelo
    public string NombreModelo { get; set; } = string.Empty;
    public string SapPartNumberFG { get; set; } = string.Empty;
    public string SapPartNumberPCBA { get; set; } = string.Empty;
    public string SapPartNumberTC { get; set; } = string.Empty;
    public string BcsPartNumber { get; set; } = string.Empty;
    public string ClientPartNumberFG { get; set; } = string.Empty;
    public string ClientPartNumberPCBA { get; set; } = string.Empty;
    public ObservableCollection<SerialEntry> Seriales { get; } = new() { new SerialEntry() };

    // 3. PCB / SAP Information
    public string PcbsPerPanel { get; set; } = "N/A";
    public ObservableCollection<PcbSapArea> AreasPcbSap { get; } = new()
    {
        new PcbSapArea { Area = "SMT" },
        new PcbSapArea { Area = "BACKEND" },
        new PcbSapArea { Area = "TLA" },
        new PcbSapArea { Area = "PACKOUT", TieneReportSide = false }
    };

    // 4. Información de línea
    public ObservableCollection<LineaInfo> Lineas { get; } = new() { new LineaInfo() };

    // 5 y 6. Diagrama de flujo (imagen adjunta por el usuario) + Interfaces MES
    public string? RutaImagenDiagramaFlujo { get; set; }
    public ObservableCollection<OperacionInfo> Operaciones { get; } = new() { new OperacionInfo() };

    // 7. Packout Setup Data
    public string PackoutTipo { get; set; } = "FG";
    public bool ValidarFlujoAntesDePack { get; set; } = true;
    public string UltimaOperacionAntesDePackout { get; set; } = string.Empty;
    public bool NuevaEstacionPackout { get; set; }
    public DateTimeOffset? FechaInstalacionEstacion { get; set; }
    public string BoxLabelQr { get; set; } = "N/A";
    public string ProductLabelQr { get; set; } = "N/A";

    // 8. Aprobaciones
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
