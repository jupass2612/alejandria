namespace Alejandria.Models;

// Un registro de alta de modelo, tal como se guarda en la base compartida.
public class ModelRecord
{
    public int Id { get; set; }

    public string NumeroModelo { get; set; } = string.Empty;

    public string NombreModelo { get; set; } = string.Empty;

    // Vacío si esa área no aplica a este modelo.
    public string NumeroParteSmt { get; set; } = string.Empty;
    public string NumeroParteBe { get; set; } = string.Empty;
    public string NumeroParteTla { get; set; } = string.Empty;

    public string QuienRecibeDocumento { get; set; } = string.Empty;

    public DateTime FechaSolicitud { get; set; }

    public string QuienMandoDocumento { get; set; } = string.Empty;

    // Nombre del PDF guardado en la carpeta "Documentos". Vacío si no hay nada.
    public string DocumentoAdjunto { get; set; } = string.Empty;

    public bool TieneDocumentoAdjunto => !string.IsNullOrWhiteSpace(DocumentoAdjunto);

    // Nombre del archivo sin el prefijo interno (Id + fecha).
    public string NombreDocumentoMostrado
    {
        get
        {
            if (!TieneDocumentoAdjunto)
            {
                return string.Empty;
            }

            // El archivo se guarda como "{Id}_{yyyyMMddHHmmss}_{nombreOriginal}.pdf".
            var partes = DocumentoAdjunto.Split('_', 3);
            return partes.Length == 3 ? partes[2] : DocumentoAdjunto;
        }
    }

    public string EtiquetaBotonDocumento => TieneDocumentoAdjunto ? "Ver PDF" : "Adjuntar PDF";

    public string FechaSolicitudTexto => FechaSolicitud.ToString("dd/MM/yyyy");

    // Áreas que aplican a este modelo, para tablas y reportes.
    public string AreasAplicablesTexto
    {
        get
        {
            var areas = AreasDisponibles;
            return areas.Count > 0 ? string.Join(", ", areas) : "—";
        }
    }

    // Áreas con número de parte. Si el modelo tiene SMT y BE capturados,
    // se combinan en una sola "SMT-BE" (mismo MODELS_MASTER en iMES, con
    // el número de BE en Model Part Number 2) en vez de dos separadas.
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

    // Área principal: SMT-BE (o SMT/BE solos) si la tiene, si no TLA.
    public string AreaPrincipal
    {
        get
        {
            var areas = AreasDisponibles;
            return areas.Count > 0 ? areas[0] : "SMT-BE";
        }
    }

    public string NumeroParteMostrado => GetNumeroParte(AreaPrincipal);

    // El número de parte "principal" del área — para SMT-BE es el de SMT
    // (el que se usa para buscar en iMES y como Model Part Number).
    public string GetNumeroParte(string area) => area switch
    {
        "SMT" => NumeroParteSmt,
        "SMT-BE" => NumeroParteSmt,
        "BE" => NumeroParteBe,
        "TLA" => NumeroParteTla,
        _ => string.Empty
    };

    // Solo aplica a SMT-BE: el número de BE, que va en Model Part Number 2.
    public string GetNumeroParteSecundario(string area) => area switch
    {
        "SMT-BE" => NumeroParteBe,
        _ => string.Empty
    };

    // Lo calculan los ViewModels (necesitan la config de todas las áreas)
    // y se guarda aquí para que la interfaz sepa que ya pasó a Histórico.
    public bool EsHistorico { get; set; }

    public bool PuedeEditar => !EsHistorico;

    // En Histórico solo se ve el botón de documento si ya había un PDF (verlo no es editar).
    public bool MostrarBotonDocumento => !EsHistorico || TieneDocumentoAdjunto;

    public bool PuedeReemplazarDocumento => TieneDocumentoAdjunto && !EsHistorico;
}
