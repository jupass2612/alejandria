using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Services;

namespace Alejandria.ViewModels;

// Configurar modelo; cada área tiene su propia config
public partial class ModelConfigViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();
    private ModelDatabaseService? _database;

    public ModelRecord Modelo { get; private set; } = new();
    public DetalleConfiguracion Detalle { get; private set; } = new();

    // Áreas con número de parte para el combo
    public List<string> AreasDisponibles { get; private set; } = new();

    [ObservableProperty]
    private string areaSeleccionada = string.Empty;

    // SMT le dice Ruta y BE/TLA Flujo al mismo ROUTE_NAME
    public bool EsAreaSmt => ConfigStatusHelper.EsAreaSmt(AreaSeleccionada);
    public bool EsAreaBeOTla => !EsAreaSmt;

    partial void OnAreaSeleccionadaChanged(string value)
    {
        OnPropertyChanged(nameof(EsAreaSmt));
        OnPropertyChanged(nameof(EsAreaBeOTla));
    }

    // Work Order/Ruta/Línea solo se capturan a mano si eligen Ingresar manual.
    // Se bloquea otra vez al cambiar de área o al encontrar algo
    [ObservableProperty]
    private bool permiteCapturaManualPlataforma;

    // x:Bind no acepta "!", va ya negada
    public bool CamposPlataformaSoloLectura => !PermiteCapturaManualPlataforma;

    partial void OnPermiteCapturaManualPlataformaChanged(bool value)
    {
        OnPropertyChanged(nameof(CamposPlataformaSoloLectura));
    }

    public IReadOnlyList<string> BdStatusOptions => ConfigStatusHelper.BdStatusOptions;
    public IReadOnlyList<string> PlataformaStatusOptions => ConfigStatusHelper.PlataformaStatusOptions;
    public IReadOnlyList<string> EstadoDocumentoOptions => ConfigStatusHelper.EstadoDocumentoOptions;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isSaving;

    [ObservableProperty]
    private bool isBuscandoImes;

    // Sin URL de iMES se oculta Buscar en iMES
    public bool ImesApiDisponible => !string.IsNullOrWhiteSpace(_settings.ImesApiBaseUrl);

    [ObservableProperty]
    private string serialPrueba = string.Empty;

    partial void OnSerialPruebaChanged(string value) => ActualizarPreviewSpecs();

    // Vista previa de MODELS_SERIALNUMBERSPECS
    public IReadOnlyList<SerialSpecPlantilla> Plantillas => SerialSpecPlantillas.Todas;

    [ObservableProperty]
    private SerialSpecPlantilla? plantillaSeleccionada;

    partial void OnPlantillaSeleccionadaChanged(SerialSpecPlantilla? value) => ActualizarPreviewSpecs();

    [ObservableProperty]
    private string modelIdTexto = string.Empty;

    // Se guarda en el área para que el Paso 3 lo use
    partial void OnModelIdTextoChanged(string value)
    {
        Detalle.ImesModelId = value?.Trim() ?? string.Empty;
        ActualizarPreviewSpecs();
    }

    public ObservableCollection<SpecPreviewFila> PreviewSpecs { get; } = new();

    [ObservableProperty]
    private string? previewSpecsMensaje = "Escribe el serial de ejemplo o sácalo del PDF.";

    public bool TienePreviewSpecsMensaje => !string.IsNullOrEmpty(PreviewSpecsMensaje);

    partial void OnPreviewSpecsMensajeChanged(string? value) => OnPropertyChanged(nameof(TienePreviewSpecsMensaje));

    public bool TienePreviewSpecs => PreviewSpecs.Count > 0;

    [ObservableProperty]
    private string sqlSpecs = string.Empty;

    public bool TieneSqlSpecs => !string.IsNullOrEmpty(SqlSpecs);

    partial void OnSqlSpecsChanged(string value) => OnPropertyChanged(nameof(TieneSqlSpecs));

    // Recalcula la tabla y el INSERT
    private void ActualizarPreviewSpecs()
    {
        PreviewSpecs.Clear();
        SqlSpecs = string.Empty;

        try
        {
            // En orden: serial, plantilla y MODELID
            var serial = SerialPrueba.Trim();
            if (string.IsNullOrEmpty(serial))
            {
                PreviewSpecsMensaje = "Escribe el serial de ejemplo o sácalo del PDF.";
                return;
            }

            if (PlantillaSeleccionada is null)
            {
                PreviewSpecsMensaje = "Elige la plantilla del cliente.";
                return;
            }

            var (filas, error) = PlantillaSeleccionada.Previsualizar(serial);
            if (error is not null)
            {
                PreviewSpecsMensaje = error;
                return;
            }

            foreach (var fila in filas)
            {
                PreviewSpecs.Add(fila);
            }

            if (!int.TryParse(ModelIdTexto.Trim(), out var modelId) || modelId <= 0)
            {
                PreviewSpecsMensaje = "Captura el MODELID (el ID del modelo en MODELS_MASTER) para armar el INSERT.";
                return;
            }

            SqlSpecs = PlantillaSeleccionada.GenerarSql(serial, modelId);
            PreviewSpecsMensaje = null;
        }
        finally
        {
            OnPropertyChanged(nameof(TienePreviewSpecs));
        }
    }

    [ObservableProperty]
    private bool isProbandoSerial;

    public bool PuedeProbarSerial => !IsProbandoSerial;

    partial void OnIsProbandoSerialChanged(bool value)
    {
        OnPropertyChanged(nameof(PuedeProbarSerial));
    }

    [ObservableProperty]
    private string? resultadoSerialMensaje;

    public bool TieneResultadoSerial => !string.IsNullOrEmpty(ResultadoSerialMensaje);

    // Payload completo que regresó iMES
    public ObservableCollection<ImesCampoPayload> PayloadSerial { get; } = new();

    partial void OnResultadoSerialMensajeChanged(string? value)
    {
        OnPropertyChanged(nameof(TieneResultadoSerial));
    }

    // Si iMES reconoció el serial
    [ObservableProperty]
    private bool resultadoSerialEncontrado;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? successMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    // Pasos: 1 Parents, 2 Master, 3 SerialSpecs, 4 Plataforma
    public const int TotalPasos = 4;

    [ObservableProperty]
    private int pasoActual = 1;

    public bool EsPaso1 => PasoActual == 1;
    public bool EsPaso2 => PasoActual == 2;
    public bool EsPaso3 => PasoActual == 3;
    public bool EsPaso4 => PasoActual == 4;
    public bool PuedeAnterior => PasoActual > 1;
    public bool PuedeSiguiente => PasoActual < TotalPasos;

    partial void OnPasoActualChanged(int value)
    {
        OnPropertyChanged(nameof(EsPaso1));
        OnPropertyChanged(nameof(EsPaso2));
        OnPropertyChanged(nameof(EsPaso3));
        OnPropertyChanged(nameof(EsPaso4));
        OnPropertyChanged(nameof(PuedeAnterior));
        OnPropertyChanged(nameof(PuedeSiguiente));
    }

    public void IrAPaso(int paso) => PasoActual = Math.Clamp(paso, 1, TotalPasos);

    [ObservableProperty]
    private string estatusPaso1 = ConfigStatusHelper.Pendiente;

    [ObservableProperty]
    private string estatusPaso2 = ConfigStatusHelper.Pendiente;

    [ObservableProperty]
    private string estatusPaso3 = ConfigStatusHelper.Pendiente;

    [ObservableProperty]
    private string estatusPaso4 = ConfigStatusHelper.Pendiente;

    // Primer paso que falta
    private int PrimerPasoPendiente()
    {
        var estatus = new[] { EstatusPaso1, EstatusPaso2, EstatusPaso3, EstatusPaso4 };
        var indice = Array.FindIndex(estatus, e => e != ConfigStatusHelper.Terminado);
        return indice < 0 ? 1 : indice + 1;
    }

    [ObservableProperty]
    private string estatusPlataformaImes = ConfigStatusHelper.Pendiente;

    [ObservableProperty]
    private string estatusGeneral = ConfigStatusHelper.Pendiente;

    // Estatus de todas las áreas
    [ObservableProperty]
    private string estatusGeneralModelo = ConfigStatusHelper.Pendiente;

    // Otra área con el mismo número de parte y config, para copiarla
    [ObservableProperty]
    private string? areaConfigCopiable;

    [ObservableProperty]
    private string? numeroParteCompartido;

    public bool TieneConfigCopiable => !string.IsNullOrEmpty(AreaConfigCopiable);

    public string MensajeConfigCopiable => AreaConfigCopiable is null
        ? string.Empty
        : $"El área {AreaConfigCopiable} ya tiene una configuración capturada para el número de parte {NumeroParteCompartido} — puedes copiarla en vez de repetir el trabajo.";

    partial void OnAreaConfigCopiableChanged(string? value)
    {
        OnPropertyChanged(nameof(TieneConfigCopiable));
        OnPropertyChanged(nameof(MensajeConfigCopiable));
    }

    [ObservableProperty]
    private DateTime fechaLimite;

    [ObservableProperty]
    private int diasRetraso;

    [ObservableProperty]
    private bool estaAtrasado;

    public string FechaLimiteTexto => FechaLimite.ToString("dd/MM/yyyy");

    public string RetrasoTexto => EstaAtrasado
        ? $"{DiasRetraso} día{(DiasRetraso == 1 ? string.Empty : "s")} de retraso"
        : "En plazo";

    partial void OnFechaLimiteChanged(DateTime value) => OnPropertyChanged(nameof(FechaLimiteTexto));

    partial void OnDiasRetrasoChanged(int value) => OnPropertyChanged(nameof(RetrasoTexto));

    partial void OnEstaAtrasadoChanged(bool value) => OnPropertyChanged(nameof(RetrasoTexto));

    public async Task InitializeAsync(ModelRecord modelo)
    {
        Modelo = modelo;
        AreasDisponibles = modelo.AreasDisponibles;
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _settings.Load();
            _database = new ModelDatabaseService(_settings.DatabasePath);
            await _database.InitializeAsync();

            // Abre en la primera área que tenga algo pendiente
            var areaInicial = await ElegirAreaInicialAsync(modelo);
            await CargarAreaAsync(areaInicial);
        }
        catch (Exception ex)
        {
            LogService.LogError("ModelConfigViewModel.InitializeAsync", ex);
            ErrorMessage = $"No se pudo cargar la configuración: {LogService.Redactar(ex.Message)}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<string> ElegirAreaInicialAsync(ModelRecord modelo)
    {
        if (_database is null || modelo.AreasDisponibles.Count == 0)
        {
            return modelo.AreaPrincipal;
        }

        var detalles = await _database.GetDetallesDeModeloAsync(modelo.Id);
        var detallesPorArea = detalles.ToDictionary(d => d.Area, d => d);

        foreach (var area in modelo.AreasDisponibles)
        {
            detallesPorArea.TryGetValue(area, out var detalle);
            detalle ??= new DetalleConfiguracion { ModeloId = modelo.Id, Area = area };

            var resumen = ConfigStatusHelper.ComputeResumen(modelo, detalle);
            if (resumen.EstatusGeneral != ConfigStatusHelper.Terminado)
            {
                return area;
            }
        }

        // Si todo está completo, la principal
        return modelo.AreaPrincipal;
    }

    // Carga la config del área o una en blanco
    public async Task CargarAreaAsync(string area)
    {
        if (_database is null)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var existing = await _database.GetDetalleAsync(Modelo.Id, area);
            Detalle = existing ?? new DetalleConfiguracion { ModeloId = Modelo.Id, Area = area };
            AreaSeleccionada = area;
            ModelIdTexto = Detalle.ImesModelId;
            PermiteCapturaManualPlataforma = false;

            RecomputeStatuses();
            PasoActual = PrimerPasoPendiente();
            await RefreshDerivedDataAsync();
        }
        catch (Exception ex)
        {
            LogService.LogError("ModelConfigViewModel.CargarAreaAsync", ex);
            ErrorMessage = $"No se pudo cargar la configuración del área {area}: {LogService.Redactar(ex.Message)}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Estatus del modelo y si otra área comparte número de parte
    private async Task RefreshDerivedDataAsync()
    {
        if (_database is null)
        {
            return;
        }

        try
        {
            var todasLasAreas = await _database.GetDetallesDeModeloAsync(Modelo.Id);

            var resumenModelo = ConfigStatusHelper.ComputeResumenModelo(Modelo, todasLasAreas);
            EstatusGeneralModelo = resumenModelo.EstatusGeneral;

            AreaConfigCopiable = null;
            NumeroParteCompartido = null;

            var numeroActual = Modelo.GetNumeroParte(AreaSeleccionada);
            if (string.IsNullOrWhiteSpace(numeroActual))
            {
                return;
            }

            foreach (var otraArea in AreasDisponibles)
            {
                if (otraArea == AreaSeleccionada)
                {
                    continue;
                }

                if (Modelo.GetNumeroParte(otraArea) != numeroActual)
                {
                    continue;
                }

                var detalleOtraArea = todasLasAreas.FirstOrDefault(d => d.Area == otraArea);
                if (detalleOtraArea is { TieneDatos: true })
                {
                    AreaConfigCopiable = otraArea;
                    NumeroParteCompartido = numeroActual;
                    break;
                }
            }
        }
        catch (Exception)
        {
            // No es grave, se queda el último valor
        }
    }

    public void RecomputeStatuses()
    {
        var resumen = ConfigStatusHelper.ComputeResumen(Modelo, Detalle);

        EstatusPlataformaImes = resumen.EstatusPlataforma;
        EstatusGeneral = resumen.EstatusGeneral;

        var pasos = ConfigStatusHelper.EstatusPasos(Detalle);
        EstatusPaso1 = pasos[0];
        EstatusPaso2 = pasos[1];
        EstatusPaso3 = pasos[2];
        EstatusPaso4 = pasos[3];

        FechaLimite = resumen.FechaLimite;
        DiasRetraso = resumen.DiasRetraso;
        EstaAtrasado = DiasRetraso > 0;

        OnPropertyChanged(nameof(ResumenFaltantes));
        OnPropertyChanged(nameof(ResumenFaltantesTono));
        OnPropertyChanged(nameof(ResumenFaltantesGlyph));
    }

    // Para el resumen de qué falta
    private List<string> CamposFaltantes
    {
        get
        {
            var faltantes = new List<string>();

            if (!ConfigStatusHelper.IsFilled(Detalle.ImesModelsParents)) faltantes.Add("MODELS_PARENTS");
            if (!ConfigStatusHelper.IsFilled(Detalle.ImesModelsMaster)) faltantes.Add("MODELS_MASTER");
            if (!ConfigStatusHelper.IsFilled(Detalle.ImesModelsSerialNumberSpecs)) faltantes.Add("MODELS_SERIALNUMBERSPECS");
            if (!ConfigStatusHelper.IsFilled(Detalle.ImesModelsSerialNumberSpecsPacking)) faltantes.Add("MODELS_SERIALNUMBERSPECS Packing");
            if (!ConfigStatusHelper.IsFilled(Detalle.WorkOrder)) faltantes.Add("Work Order");
            if (EsAreaSmt)
            {
                if (!ConfigStatusHelper.IsFilled(Detalle.Ruta)) faltantes.Add("Ruta");
            }
            else
            {
                if (!ConfigStatusHelper.IsFilled(Detalle.Flujo)) faltantes.Add("Flujo");
            }
            if (!ConfigStatusHelper.IsFilled(Detalle.Linea)) faltantes.Add("Línea");
            if (!ConfigStatusHelper.IsFilled(Detalle.PartLineBomMaintenance)) faltantes.Add("PartLineBomMaintenance");
            if (!Modelo.TieneDocumentoAdjunto) faltantes.Add("PDF adjunto (en Altas de Modelos)");

            return faltantes;
        }
    }

    public string ResumenFaltantes
    {
        get
        {
            var faltantes = CamposFaltantes;
            return faltantes.Count == 0
                ? "Esta área ya tiene todo capturado."
                : $"Falta: {string.Join(", ", faltantes)}";
        }
    }

    // Color con KpiTonoToBrushConverter
    public string ResumenFaltantesTono => CamposFaltantes.Count == 0 ? "Success" : "Warning";

    // Palomita si está completo, aviso si falta algo
    public string ResumenFaltantesGlyph => CamposFaltantes.Count == 0 ? "\uE73E" : "\uE7BA";

    // Work orders del número de parte, solo los del área (por LINE).
    // Puede haber varios, uno por línea
    public async Task<List<ImesWorkOrder>> BuscarEnImesAsync()
    {
        var numeroParte = Modelo.GetNumeroParte(AreaSeleccionada);
        if (string.IsNullOrWhiteSpace(numeroParte))
        {
            ErrorMessage = $"El área {AreaSeleccionada} no tiene número de parte capturado.";
            return new List<ImesWorkOrder>();
        }

        IsBuscandoImes = true;
        ErrorMessage = null;

        try
        {
            var imes = new ImesApiService(_settings.ImesApiBaseUrl, _settings.ImesApiBearerToken);
            var todos = await imes.BuscarWorkOrdersAsync(numeroParte);
            var delArea = ImesApiService.FiltrarPorArea(todos, AreaSeleccionada);

            if (delArea.Count == 0)
            {
                ErrorMessage = todos.Count == 0
                    ? $"iMES no tiene ningún work order para el número de parte {numeroParte}. Verifica si el Work Order ya está activo en iMES."
                    : $"iMES tiene work orders para {numeroParte}, pero ninguno en una línea de {AreaSeleccionada}. Verifica si el Work Order de esa área ya está activo en iMES.";
            }

            return delArea;
        }
        catch (Exception ex)
        {
            LogService.LogError("ModelConfigViewModel.BuscarEnImesAsync", ex);
            ErrorMessage = $"No se pudo consultar iMES: {LogService.Redactar(ex.Message)}";
            return new List<ImesWorkOrder>();
        }
        finally
        {
            IsBuscandoImes = false;
        }
    }

    // Pasa lo de iMES a pantalla sin guardar; las líneas se juntan separadas por coma
    public void AplicarWorkOrdersImes(List<ImesWorkOrder> workOrders)
    {
        if (workOrders.Count == 0)
        {
            return;
        }

        // Ya hay datos de iMES, se bloquea la captura manual
        PermiteCapturaManualPlataforma = false;

        var lineas = workOrders
            .Select(wo => wo.Line)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var workOrdersDistintos = workOrders
            .Select(wo => wo.WorkOrder)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rutas = workOrders
            .Select(wo => wo.RouteName)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Detalle.WorkOrder = workOrdersDistintos.FirstOrDefault() ?? string.Empty;

        // SMT lo guarda en Ruta, BE/TLA en Flujo
        if (EsAreaSmt)
        {
            Detalle.Ruta = rutas.FirstOrDefault() ?? string.Empty;
        }
        else
        {
            Detalle.Flujo = rutas.FirstOrDefault() ?? string.Empty;
        }

        Detalle.Linea = string.Join(", ", lineas);
        RecomputeStatuses();

        // Si salen varios WO distintos se avisa, puede haber viejos activos
        var aviso = workOrdersDistintos.Count > 1
            ? $" Aviso: iMES tiene {workOrdersDistintos.Count} work orders distintos para estas líneas (se usó \"{Detalle.WorkOrder}\") — puede que haya work orders con el nombre anterior todavía activos; verifica que sea el correcto."
            : string.Empty;

        SuccessMessage = lineas.Count > 1
            ? $"Se aplicó desde iMES — {lineas.Count} líneas encontradas ({Detalle.Linea}).{aviso} Revisa y guarda para confirmarlo."
            : $"Se aplicó desde iMES ({Detalle.Linea}).{aviso} Revisa y guarda para confirmarlo.";
    }

    // Prueba el serial en iMES
    public async Task ProbarSerialAsync()
    {
        ResultadoSerialMensaje = null;
        PayloadSerial.Clear();

        var serial = SerialPrueba.Trim();
        if (string.IsNullOrWhiteSpace(serial))
        {
            ResultadoSerialEncontrado = false;
            ResultadoSerialMensaje = "Escribe el serial de ejemplo arriba primero.";
            return;
        }

        IsProbandoSerial = true;

        try
        {
            var imes = new ImesApiService(_settings.ImesApiBaseUrl, _settings.ImesApiBearerToken);
            var (modelo, camposCompletos) = await imes.ObtenerModeloPorSerialConPayloadAsync(serial);

            foreach (var (campo, valor) in camposCompletos)
            {
                PayloadSerial.Add(new ImesCampoPayload { Campo = campo, Valor = valor });
            }

            if (modelo is null)
            {
                ResultadoSerialEncontrado = false;
                ResultadoSerialMensaje = "Serial no reconocido por iMES — revisa las especificaciones del serial.";
                return;
            }

            var numerosParte = string.IsNullOrWhiteSpace(modelo.ModelPartNumber2)
                ? modelo.ModelPartNumber
                : $"{modelo.ModelPartNumber}, {modelo.ModelPartNumber2}";

            ResultadoSerialEncontrado = true;
            ResultadoSerialMensaje =
                $"\"{modelo.ModelName}\" tiene el modelo padre \"{modelo.ParentModelName}\" — números de parte: {numerosParte}.";
        }
        catch (Exception ex)
        {
            LogService.LogError("ModelConfigViewModel.ProbarSerialAsync", ex);
            ResultadoSerialEncontrado = false;
            ResultadoSerialMensaje = $"No se pudo consultar iMES: {LogService.Redactar(ex.Message)}";
        }
        finally
        {
            IsProbandoSerial = false;
        }
    }

    // Seriales de ejemplo del PDF adjunto
    public List<SerialCandidato> ExtraerCandidatosSerialDelPdf()
    {
        ErrorMessage = null;

        if (!Modelo.TieneDocumentoAdjunto)
        {
            ErrorMessage = "Este modelo no tiene un PDF adjunto todavía (usa \"Adjuntar PDF\" en Altas de Modelos).";
            return new List<SerialCandidato>();
        }

        try
        {
            var rutaPdf = DocumentAttachmentService.ObtenerRutaCompleta(_settings.DatabasePath, Modelo.DocumentoAdjunto);
            var candidatos = PdfSerialExtractorService.ExtraerCandidatosSerial(rutaPdf);

            if (candidatos.Count == 0)
            {
                ErrorMessage = "No se encontró un \"Laser Serial Number real example\" en el PDF adjunto, escríbelo a mano.";
            }

            return candidatos;
        }
        catch (Exception ex)
        {
            LogService.LogError("ModelConfigViewModel.ExtraerCandidatosSerialDelPdf", ex);
            ErrorMessage = $"No se pudo leer el PDF adjunto: {LogService.Redactar(ex.Message)}";
            return new List<SerialCandidato>();
        }
    }

    public async Task<bool> SaveAsync()
    {
        if (_database is null)
        {
            ErrorMessage = "La base de datos no está disponible.";
            return false;
        }

        IsSaving = true;
        ErrorMessage = null;
        SuccessMessage = null;

        try
        {
            await _database.SaveDetalleAsync(Detalle);
            SuccessMessage = $"Configuración de {Detalle.Area} guardada.";
            await RefreshDerivedDataAsync();
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("ModelConfigViewModel.SaveAsync", ex);
            ErrorMessage = $"No se pudo guardar la configuración: {LogService.Redactar(ex.Message)}";
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    // Copia en memoria la config de otra área (no guarda)
    public async Task<bool> CopiarConfiguracionDeAsync(string areaOrigen)
    {
        if (_database is null)
        {
            ErrorMessage = "La base de datos no está disponible.";
            return false;
        }

        try
        {
            var origen = await _database.GetDetalleAsync(Modelo.Id, areaOrigen);
            if (origen is null)
            {
                return false;
            }

            // Ruta/Flujo se pasa al campo que toque según el área destino
            var valorRutaOFlujoOrigen = ConfigStatusHelper.EsAreaSmt(origen.Area) ? origen.Ruta : origen.Flujo;

            Detalle = new DetalleConfiguracion
            {
                ModeloId = Modelo.Id,
                Area = AreaSeleccionada,
                ImesModelsParents = origen.ImesModelsParents,
                ImesModelsMaster = origen.ImesModelsMaster,
                ImesModelsSerialNumberSpecs = origen.ImesModelsSerialNumberSpecs,
                ImesModelsSerialNumberSpecsPacking = origen.ImesModelsSerialNumberSpecsPacking,
                WorkOrder = origen.WorkOrder,
                Flujo = EsAreaSmt ? string.Empty : valorRutaOFlujoOrigen,
                Ruta = EsAreaSmt ? valorRutaOFlujoOrigen : string.Empty,
                Linea = origen.Linea,
                PartLineBomMaintenance = origen.PartLineBomMaintenance,
                ImesModelId = origen.ImesModelId
            };

            ModelIdTexto = Detalle.ImesModelId;
            AreaConfigCopiable = null;
            RecomputeStatuses();
            SuccessMessage = $"Se copió la configuración de {areaOrigen}. Revísala y guarda para confirmarla.";
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("ModelConfigViewModel.CopiarConfiguracionDeAsync", ex);
            ErrorMessage = $"No se pudo copiar la configuración de {areaOrigen}: {LogService.Redactar(ex.Message)}";
            return false;
        }
    }
}
