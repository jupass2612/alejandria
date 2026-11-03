namespace Alejandria.Models;

// Alta de modelo como se guarda en la base
public class ModelRecord
{
    public int Id { get; set; }

    public string NumeroModelo { get; set; } = string.Empty;

    public string NombreModelo { get; set; } = string.Empty;

    // Vacío si el área no aplica
    public string NumeroParteSmt { get; set; } = string.Empty;
    public string NumeroParteBe { get; set; } = string.Empty;
    public string NumeroParteTla { get; set; } = string.Empty;

    public string QuienRecibeDocumento { get; set; } = string.Empty;

    public DateTime FechaSolicitud { get; set; }

    public string QuienMandoDocumento { get; set; } = string.Empty;

    // Nombre del PDF en la carpeta Documentos, vacío si no hay
    public string DocumentoAdjunto { get; set; } = string.Empty;

    public bool TieneDocumentoAdjunto => !string.IsNullOrWhiteSpace(DocumentoAdjunto);

    // Nombre del archivo sin el prefijo (Id + fecha)
    public string NombreDocumentoMostrado
    {
        get
        {
            if (!TieneDocumentoAdjunto)
            {
                return string.Empty;
            }

            // Se guarda como {Id}_{yyyyMMddHHmmss}_{nombre}.pdf
            var partes = DocumentoAdjunto.Split('_', 3);
            return partes.Length == 3 ? partes[2] : DocumentoAdjunto;
        }
    }

    public string EtiquetaBotonDocumento => TieneDocumentoAdjunto ? "Ver PDF" : "Adjuntar PDF";

    public string FechaSolicitudTexto => FechaSolicitud.ToString("dd/MM/yyyy");

    // Áreas del modelo para tablas y reportes
    public string AreasAplicablesTexto
    {
        get
        {
            var areas = AreasDisponibles;
            return areas.Count > 0 ? string.Join(", ", areas) : "—";
        }
    }

    // Áreas con número de parte. SMT y BE juntos son SMT-BE (mismo MODELS_MASTER, BE va en Part Number 2)
    public List<string> AreasDisponibles
    {
        get
        {
            var areas = new List<string>();
            var tieneSmt = !string.IsNullOrWhiteSpace(NumeroParteSmt);
            var tieneBe = !string.IsNullOrWhiteSpace(NumeroParteBe);

            if (tieneSmt && tieneBe)
            {
                areas.Add("SMT-BE");
            }
            else if (tieneSmt)
            {
                areas.Add("SMT");
            }
            else if (tieneBe)
            {
                areas.Add("BE");
            }

            if (!string.IsNullOrWhiteSpace(NumeroParteTla)) areas.Add("TLA");
            return areas;
        }
    }

    // SMT-BE (o SMT/BE) si hay, si no TLA
    public string AreaPrincipal
    {
        get
        {
            var areas = AreasDisponibles;
            return areas.Count > 0 ? areas[0] : "SMT-BE";
        }
    }

    public string NumeroParteMostrado => GetNumeroParte(AreaPrincipal);

    // Número de parte principal del área; en SMT-BE es el de SMT
    public string GetNumeroParte(string area) => area switch
    {
        "SMT" => NumeroParteSmt,
        "SMT-BE" => NumeroParteSmt,
        "BE" => NumeroParteBe,
        "TLA" => NumeroParteTla,
        _ => string.Empty
    };

    // Solo SMT-BE: el número de BE para Part Number 2
    public string GetNumeroParteSecundario(string area) => area switch
    {
        "SMT-BE" => NumeroParteBe,
        _ => string.Empty
    };

    // Lo ponen los ViewModels para saber si ya está en Histórico
    public bool EsHistorico { get; set; }

    public bool PuedeEditar => !EsHistorico;

    // En Histórico solo si ya tiene PDF (verlo no es editar)
    public bool MostrarBotonDocumento => !EsHistorico || TieneDocumentoAdjunto;

    public bool PuedeReemplazarDocumento => TieneDocumentoAdjunto && !EsHistorico;

    // Si el menú ⋯ tiene algo que mostrar
    public bool TieneAcciones => PuedeEditar || MostrarBotonDocumento;
}
