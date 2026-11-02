using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Services;

namespace Alejandria.ViewModels;

// ViewModel de "Configurar modelo": base de datos iMES y Plataforma iMES.
// Cada área (SMT, BE, TLA) tiene su propia config, por eso hay un combo de área.
public partial class ModelConfigViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();
    private ModelDatabaseService? _database;

    public ModelRecord Modelo { get; private set; } = new();
    public DetalleConfiguracion Detalle { get; private set; } = new();

    // Áreas con número de parte, para llenar el combo de área.
    public List<string> AreasDisponibles { get; private set; } = new();

    [ObservableProperty]
    private string areaSeleccionada = string.Empty;

    // SMT le dice "Ruta" al dato de iMES (ROUTE_NAME); BE y TLA le dicen
    // "Flujo" al mismo dato — controla qué campo se muestra en pantalla.
    public bool EsAreaSmt => ConfigStatusHelper.EsAreaSmt(AreaSeleccionada);
    public bool EsAreaBeOTla => !EsAreaSmt;

    partial void OnAreaSeleccionadaChanged(string value)
    {
        OnPropertyChanged(nameof(EsAreaSmt));
        OnPropertyChanged(nameof(EsAreaBeOTla));
    }

    // Work Order/Ruta/Línea normalmente son de solo lectura (se llenan con
    // "Buscar en iMES") — esto se pone en true solo cuando el usuario elige
    // "Ingresar manual" en el aviso que sale si esa búsqueda no encuentra
    // nada. Se vuelve a bloquear al cambiar de área o al encontrar algo en iMES.
    [ObservableProperty]
    private bool permiteCapturaManualPlataforma;

    // El compilador de XAML de esta versión del Windows App SDK no admite
    // "!" dentro de x:Bind — se expone ya negada para poder enlazarla
    // directo a IsReadOnly sin necesitar un converter aparte.
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

    // Si no hay URL de iMES en appsettings.json, se oculta el botón "Buscar en iMES".
    public bool ImesApiDisponible => !string.IsNullOrWhiteSpace(_settings.ImesApiBaseUrl);

    [ObservableProperty]
    private string serialPrueba = string.Empty;

    partial void OnSerialPruebaChanged(string value) => ActualizarPreviewSpecs();

    // Vista previa de MODELS_SERIALNUMBERSPECS con el mismo serial
    public IReadOnlyList<SerialSpecPlantilla> Plantillas => SerialSpecPlantillas.Todas;

    [ObservableProperty]
    private SerialSpecPlantilla? plantillaSeleccionada;

    partial void OnPlantillaSeleccionadaChanged(SerialSpecPlantilla? value) => ActualizarPreviewSpecs();

    [ObservableProperty]
    private string modelIdTexto = string.Empty;

    // Se guarda en el área para que el Paso 3 lo use sin volver a escribirlo
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

    // Recalcula la tabla y el INSERT con lo que haya capturado
    private void ActualizarPreviewSpecs()
    {
        PreviewSpecs.Clear();
        SqlSpecs = string.Empty;

        try
        {
            // Se piden en orden: serial, plantilla y MODELID
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

    // El payload completo que regresó iMES, para revisar qué contestó
    public ObservableCollection<ImesCampoPayload> PayloadSerial { get; } = new();

    partial void OnResultadoSerialMensajeChanged(string? value)
    {
        OnPropertyChanged(nameof(TieneResultadoSerial));
    }

    // Si el último serial se reconoció en iMES (color del mensaje y payload)
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

    // Configurar modelo va por pasos: 1 Parents, 2 Master, 3 SerialSpecs, 4 Plataforma
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

    // Primer paso que falta, para abrir directo ahí
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

    // Estatus de TODO el modelo (todas las áreas), no solo la que se ve ahora.
    [ObservableProperty]
    private string estatusGeneralModelo = ConfigStatusHelper.Pendiente;

    // Otra área con el mismo número de parte y config ya capturada, para
    // ofrecer copiarla en vez de repetir el trabajo. Null si no aplica.
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

            // Arranca en la primera área que todavía tenga algo pendiente
            // (en vez de siempre la principal) — así se llega directo a lo
            // que falta, en vez de tener que cambiar de área a mano.
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

        // Todas las áreas ya están completas — se abre en la principal, como antes.
        return modelo.AreaPrincipal;
    }

    // Carga la config de un área (o crea una en blanco si no tiene nada
    // todavía). Se usa al abrir la pantalla y al cambiar de área.
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

    // Recalcula el estatus de todo el modelo y checa si otra área comparte
    // número de parte con la actual (para ofrecer copiar su config).
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
            // No es grave, se deja el último valor conocido.
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

    // Para el resumen de "qué falta" arriba de la pantalla — evita tener
    // que recorrer toda la página para ver qué le hace falta a esta área.
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

    // Para el color del texto/ícono (reutiliza KpiTonoToBrushConverter, que
    // ya sabe pintar "Success"/"Warning").
    public string ResumenFaltantesTono => CamposFaltantes.Count == 0 ? "Success" : "Warning";

    // Glifo de Segoe Fluent Icons: palomita si ya está completo, aviso si falta algo.
    public string ResumenFaltantesGlyph => CamposFaltantes.Count == 0 ? "\uE73E" : "\uE7BA";

    // Busca en iMES los work orders del número de parte actual y deja solo
    // los del área elegida (por el campo LINE, no por el nombre del WO).
    // Puede haber varios (varias líneas físicas de la misma área).
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

    // Copia lo encontrado en iMES a la pantalla (no guarda todavía). El
    // Work Order y la Ruta se repiten entre líneas de la misma área, solo
    // cambia la Línea, así que se juntan todas separadas por coma.
    public void AplicarWorkOrdersImes(List<ImesWorkOrder> workOrders)
    {
        if (workOrders.Count == 0)
        {
            return;
        }

        // Ya hay datos reales de iMES — se vuelve a bloquear la captura manual.
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

        // SMT le dice "Ruta" a este dato; BE y TLA le dicen "Flujo" —
        // mismo ROUTE_NAME de iMES, solo cambia dónde se guarda según el área.
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

        // Si salió más de un Work Order distinto, probablemente hay
        // work orders viejos mezclados con nuevos — se avisa en vez de
        // elegir uno en silencio.
        var aviso = workOrdersDistintos.Count > 1
            ? $" Aviso: iMES tiene {workOrdersDistintos.Count} work orders distintos para estas líneas (se usó \"{Detalle.WorkOrder}\") — puede que haya work orders con el nombre anterior todavía activos; verifica que sea el correcto."
            : string.Empty;

        SuccessMessage = lineas.Count > 1
            ? $"Se aplicó desde iMES — {lineas.Count} líneas encontradas ({Detalle.Linea}).{aviso} Revisa y guarda para confirmarlo."
            : $"Se aplicó desde iMES ({Detalle.Linea}).{aviso} Revisa y guarda para confirmarlo.";
    }

    // Prueba un serial contra iMES para corroborar MODELS_SERIALNUMBERSPECS y su Packing.
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

    // Saca del PDF adjunto los candidatos a serial de ejemplo
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

    // Copia en memoria la config de otra área con el mismo número de
    // parte (no guarda todavía).
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

            // El dato de ruta/flujo puede venir del campo con el nombre
            // distinto si el área de origen es de otro tipo (SMT le dice
            // "Ruta", BE/TLA le dicen "Flujo" al mismo valor) — se traduce
            // al campo que le corresponde al área de destino.
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
