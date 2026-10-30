using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Services;

namespace Alejandria.ViewModels;

// Un resultado de "buscar parents parecidos" — el candidato y qué tan
// parecido es (0 a 1), para mostrarlo en pantalla como porcentaje.
public class ParentSimilarResult
{
    public ParentSimilarResult(ImesParentModel parent, double similitud)
    {
        Parent = parent;
        Similitud = similitud;
    }

    public ImesParentModel Parent { get; }
    public double Similitud { get; }
    public string SimilitudTexto => $"{Similitud:P0} parecido";
}

// ViewModel de "Configurar en iMES" (experimental) — guía los 3 pasos
// para dar de alta un modelo directo en Oracle: Parent, Master, y
// SerialNumberSpecs. Cada paso ofrece "usar uno parecido/existente" antes
// de forzar a capturar todo desde cero.
public partial class ConfigurarEnImesViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();
    private ImesOracleModelService? _service;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [ObservableProperty]
    private bool isBusy;

    // ---- Paso 1: Parent ----

    [ObservableProperty]
    private string nombreParentBuscado = string.Empty;

    public ObservableCollection<ParentSimilarResult> ParentsSimilares { get; } = new();

    // Todos los "parents" activos, para el desplegable de PARENTMODELID
    // en el Paso 2 — se carga al entrar a ese paso.
    public ObservableCollection<ImesParentModel> TodosLosParents { get; } = new();

    public async Task CargarTodosLosParentsAsync()
    {
        if (_service is null)
        {
            return;
        }

        try
        {
            var parents = await _service.ObtenerTodosLosParentsAsync();
            TodosLosParents.Clear();
            foreach (var parent in parents)
            {
                TodosLosParents.Add(parent);
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.CargarTodosLosParentsAsync", ex);
            ErrorMessage = $"No se pudo cargar la lista de modelos padre: {LogService.Redactar(ex.Message)}";
        }
    }

    [ObservableProperty]
    private ImesParentModel? parentSeleccionado;

    // Campos para crear un Parent nuevo (cuando ninguno de los
    // parecidos es el correcto).
    public ImesParentModel NuevoParent { get; } = new() { IsActive = true };

    // ---- Paso 2: Master ----

    [ObservableProperty]
    private string numeroDeParteBuscado = string.Empty;

    public ObservableCollection<ImesMasterModel> MastersConPrefijoIgual { get; } = new();

    // El modelo del que se copiaron los datos (si se basó en uno
    // existente) — se usa en el Paso 3 para copiar también sus specs.
    [ObservableProperty]
    private ImesMasterModel? masterBase;

    public ImesMasterModel NuevoMaster { get; } = new() { IsActive = true };

    [ObservableProperty]
    private ImesMasterModel? masterCreado;

    // ---- Paso 3: SerialNumberSpecs ----

    public ObservableCollection<ImesSerialNumberSpec> SpecsDelModeloBase { get; } = new();
    public ObservableCollection<ImesSerialNumberSpec> SpecsNuevos { get; } = new();

    public void Initialize()
    {
        _settings.Load();
        var entorno = _settings.ImesOracleEntornoSeleccionado;
        _service = entorno is not null ? new ImesOracleModelService(entorno) : null;

        if (_service is null)
        {
            ErrorMessage = "No hay un entorno de iMES configurado (ver Ajustes).";
        }
    }

    // Modelo y área para los que se está dando de alta — así se
    // precargan los datos y, al terminar cada paso, se marca solo el
    // seguimiento local (Histórico/Estadísticas/reportes siguen leyendo
    // lo mismo de siempre, ya no hace falta marcarlo a mano).
    public ModelRecord? Modelo { get; private set; }
    public string Area { get; private set; } = string.Empty;

    public string[] ResponsablesDisponibles => ConfigStatusHelper.Responsables;

    // Cada paso pregunta "¿quién hizo esto?" por separado — no todos los
    // pasos los hace necesariamente la misma persona, y preguntarlo una
    // sola vez al principio dejaba pasos sin marcar si se creaba algo
    // antes de elegir a alguien.
    [ObservableProperty]
    private string? responsableParent;

    [ObservableProperty]
    private string? responsableMaster;

    [ObservableProperty]
    private string? responsableSpecs;

    public void Initialize(ModelRecord modelo, string area)
    {
        Initialize();
        Modelo = modelo;
        Area = area;

        if (EsSmtBeCombinado)
        {
            // SMT y BE de este modelo van en el MISMO MODELS_MASTER — SMT
            // como número principal, BE en el número 2, y "(SMT-BE)" en
            // el nombre, siguiendo la convención que ya usa iMES.
            NumeroDeParteBuscado = modelo.NumeroParteSmt;
            NuevoMaster.ModelName = modelo.NombreModelo.Contains("(SMT-BE)", StringComparison.OrdinalIgnoreCase)
                ? modelo.NombreModelo
                : $"{modelo.NombreModelo} (SMT-BE)";
            NuevoMaster.ModelPartNumber = modelo.NumeroParteSmt;
            NuevoMaster.ModelPartNumber2 = modelo.NumeroParteBe;
        }
        else
        {
            var numeroParte = modelo.GetNumeroParte(area);
            NumeroDeParteBuscado = numeroParte;
            NuevoMaster.ModelName = modelo.NombreModelo;
            NuevoMaster.ModelPartNumber = numeroParte;
        }
    }

    // SMT y BE del mismo modelo suelen ir en un solo MODELS_MASTER (no
    // uno por área) — aplica cuando el modelo tiene número de parte de
    // AMBAS y la que se está viendo no es TLA (TLA siempre va aparte).
    public bool EsSmtBeCombinado =>
        Modelo is not null
        && !string.IsNullOrWhiteSpace(Modelo.NumeroParteSmt)
        && !string.IsNullOrWhiteSpace(Modelo.NumeroParteBe)
        && !string.Equals(Area, "TLA", StringComparison.OrdinalIgnoreCase);

    public string EntornoActivoTexto => _settings.ImesOracleEntornoActivo;

    // Si este modelo ya tiene un modelo padre asignado (de cualquier
    // área), lo trae de Oracle para mostrarlo como ya hecho — evita
    // volver a buscar/crear uno cada vez que se cambia de área.
    public async Task<ImesParentModel?> CargarParentRecordadoAsync()
    {
        if (_service is null || Modelo?.OracleParentId is not { } parentId)
        {
            return null;
        }

        try
        {
            var parent = await _service.ObtenerParentPorIdAsync(parentId);
            if (parent is not null)
            {
                ParentSeleccionado = parent;

                // Por si esta área en particular no se había marcado antes
                // (ej. se agregó el número de parte de esta área después de
                // haber elegido el modelo padre) — así el KPI de esta área
                // también cuenta MODELS_PARENTS como hecho.
                await MarcarDescubiertoEnSeguimientoLocalAsync(
                    d => ConfigStatusHelper.IsCompleted(d.ImesModelsParents),
                    d => d.ImesModelsParents = ValorParaMarcarDescubierto(ResponsableParent));
            }

            return parent;
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.CargarParentRecordadoAsync", ex);
            return null;
        }
    }

    // En cuanto se sabe el modelo padre, consulta directo en Oracle si ya
    // existe un MODELS_MASTER con ese mismo número de parte y ese mismo
    // padre — en vez de confiar solo en la marca local, que podría estar
    // desactualizada (ej. si se dio de alta desde otra computadora o
    // directo en Oracle). Si lo encuentra, deja el Paso 2 como ya hecho.
    public async Task<ImesMasterModel?> VerificarMasterExistenteAsync()
    {
        if (_service is null || ParentSeleccionado is null)
        {
            return null;
        }

        var numeroParte = EsSmtBeCombinado ? Modelo!.NumeroParteSmt : NumeroDeParteBuscado;
        if (string.IsNullOrWhiteSpace(numeroParte))
        {
            return null;
        }

        try
        {
            var existente = await _service.BuscarMasterExactoAsync(numeroParte, ParentSeleccionado.Id);
            if (existente is not null)
            {
                MasterCreado = existente;
                NuevoMaster.Id = existente.Id;

                // El área ya viene como "SMT-BE" combinada cuando aplica
                // (ver ModelRecord.AreasDisponibles) — no hace falta traducir
                // nada, se marca la misma área que se está viendo.
                await MarcarDescubiertoEnSeguimientoLocalAsync(
                    d => ConfigStatusHelper.IsCompleted(d.ImesModelsMaster),
                    d => d.ImesModelsMaster = ValorParaMarcarDescubierto(ResponsableMaster));
            }

            return existente;
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.VerificarMasterExistenteAsync", ex);
            return null;
        }
    }

    // Después de guardar algo en Oracle, refleja lo mismo en el
    // seguimiento local (DetalleConfiguracion) — si no hay Modelo/Area
    // (se abrió sin contexto, desde DataBase directo) no hace nada.
    // El modelo padre aplica al modelo completo (no a un área en
    // particular), así que su marca se replica a todas las áreas que
    // tenga el modelo, no solo a la que se está viendo ahorita.
    private async Task MarcarEnSeguimientoLocalAsync(string? responsablePaso, Action<DetalleConfiguracion> marcar, bool todasLasAreas = false, List<string>? areasEspecificas = null)
    {
        if (Modelo is null || string.IsNullOrWhiteSpace(Area))
        {
            // Se abrió sin contexto de modelo (desde DataBase directo) —
            // no hay nada que actualizar en el seguimiento local.
            return;
        }

        if (string.IsNullOrWhiteSpace(responsablePaso))
        {
            ErrorMessage = "Se guardó en iMES, pero no se actualizó el seguimiento local porque no elegiste quién hizo este paso.";
            return;
        }

        try
        {
            var database = new ModelDatabaseService(_settings.DatabasePath);
            await database.InitializeAsync();

            var areas = areasEspecificas ?? (todasLasAreas ? Modelo.AreasDisponibles : new List<string> { Area });
            foreach (var area in areas)
            {
                var detalle = await database.GetDetalleAsync(Modelo.Id, area) ?? new DetalleConfiguracion { ModeloId = Modelo.Id, Area = area };
                marcar(detalle);
                await database.SaveDetalleAsync(detalle);
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.MarcarEnSeguimientoLocalAsync", ex);
            ErrorMessage = $"Se guardó en iMES, pero no se pudo actualizar el seguimiento local: {LogService.Redactar(ex.Message)}";
        }
    }

    // Si ya se eligió quién hizo este paso en particular, se usa su
    // nombre (queda mejor para el historial); si no, se marca
    // "Terminado" a secas — esto es un DESCUBRIMIENTO (ya existía en
    // Oracle), no una acción que alguien acaba de tomar.
    private static string ValorParaMarcarDescubierto(string? responsablePaso) =>
        string.IsNullOrWhiteSpace(responsablePaso) ? ConfigStatusHelper.Terminado : responsablePaso!;

    // Igual que MarcarEnSeguimientoLocalAsync, pero para cuando Alejandría
    // DESCUBRE (consultando Oracle) que un paso ya estaba hecho desde
    // antes — a diferencia de una acción que el usuario acaba de tomar,
    // esto debe reflejarse en el seguimiento local (y por lo tanto en los
    // KPI de Estadísticas) sin necesitar que ya se haya elegido un
    // responsable, y sin mostrar ningún aviso si no se eligió.
    private async Task MarcarDescubiertoEnSeguimientoLocalAsync(Func<DetalleConfiguracion, bool> yaCompleto, Action<DetalleConfiguracion> marcar, List<string>? areasEspecificas = null)
    {
        if (Modelo is null || string.IsNullOrWhiteSpace(Area))
        {
            return;
        }

        try
        {
            var database = new ModelDatabaseService(_settings.DatabasePath);
            await database.InitializeAsync();

            var areas = areasEspecificas ?? new List<string> { Area };
            foreach (var area in areas)
            {
                var detalle = await database.GetDetalleAsync(Modelo.Id, area) ?? new DetalleConfiguracion { ModeloId = Modelo.Id, Area = area };

                // Si ya estaba marcado (ej. con el nombre de una persona
                // específica de una sesión anterior), no se sobreescribe
                // con el marcador genérico solo porque ahorita no se ha
                // vuelto a elegir un responsable.
                if (yaCompleto(detalle))
                {
                    continue;
                }

                marcar(detalle);
                await database.SaveDetalleAsync(detalle);
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.MarcarDescubiertoEnSeguimientoLocalAsync", ex);
        }
    }

    // Guarda a qué modelo padre quedó ligado el modelo — se recuerda
    // desde cualquier área, y su estatus se marca en todas las áreas que
    // tenga el modelo (MODELS_PARENTS es del modelo completo).
    private async Task RecordarParentYMarcarTodasLasAreasAsync(ImesParentModel parent)
    {
        if (Modelo is null)
        {
            return;
        }

        try
        {
            var database = new ModelDatabaseService(_settings.DatabasePath);
            await database.InitializeAsync();
            await database.UpdateOracleParentAsync(Modelo.Id, parent.Id, parent.ParentModelName);
            Modelo.OracleParentId = parent.Id;
            Modelo.OracleParentNombre = parent.ParentModelName;
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.RecordarParentYMarcarTodasLasAreasAsync", ex);
        }

        await MarcarEnSeguimientoLocalAsync(ResponsableParent, d => d.ImesModelsParents = ResponsableParent!, todasLasAreas: true);
    }

    // ---- Paso 1 ----

    public async Task BuscarParentsAsync()
    {
        if (_service is null || string.IsNullOrWhiteSpace(NombreParentBuscado))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var resultados = await _service.BuscarParentsSimilaresAsync(NombreParentBuscado);
            ParentsSimilares.Clear();
            foreach (var (parent, similitud) in resultados)
            {
                ParentsSimilares.Add(new ParentSimilarResult(parent, similitud));
            }

            NuevoParent.ParentModelName = NombreParentBuscado;
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.BuscarParentsAsync", ex);
            ErrorMessage = $"No se pudo buscar: {LogService.Redactar(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task<bool> ElegirParentExistente(ImesParentModel parent)
    {
        if (string.IsNullOrWhiteSpace(ResponsableParent))
        {
            ErrorMessage = "Elige quién hizo este paso antes de continuar.";
            return false;
        }

        ParentSeleccionado = parent;
        await RecordarParentYMarcarTodasLasAreasAsync(parent);
        await VerificarMasterExistenteAsync();
        return true;
    }

    public async Task<bool> CrearParentNuevoAsync()
    {
        if (_service is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ResponsableParent))
        {
            ErrorMessage = "Elige quién hizo este paso antes de crear el modelo padre.";
            return false;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var id = await _service.CrearParentAsync(NuevoParent);
            NuevoParent.Id = id;
            ParentSeleccionado = NuevoParent;
            await RecordarParentYMarcarTodasLasAreasAsync(NuevoParent);
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.CrearParentNuevoAsync", ex);
            ErrorMessage = $"No se pudo crear: {LogService.Redactar(ex.Message)}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---- Paso 2 ----

    public async Task BuscarMastersAsync()
    {
        if (_service is null || string.IsNullOrWhiteSpace(NumeroDeParteBuscado))
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var resultados = await _service.BuscarMasterPorPrefijoAsync(NumeroDeParteBuscado);
            MastersConPrefijoIgual.Clear();
            foreach (var resultado in resultados)
            {
                MastersConPrefijoIgual.Add(resultado);
            }

            NuevoMaster.ModelPartNumber = NumeroDeParteBuscado;
            NuevoMaster.ParentModelId = ParentSeleccionado?.Id;
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.BuscarMastersAsync", ex);
            ErrorMessage = $"No se pudo buscar: {LogService.Redactar(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Copia los valores del existente a NuevoMaster, como punto de
    // partida editable — no crea nada todavía, eso pasa al "Guardar".
    public void BasarseEnMasterExistente(ImesMasterModel origen)
    {
        MasterBase = origen;

        NuevoMaster.ModelName = origen.ModelName;
        NuevoMaster.ModelDesc = origen.ModelDesc;
        NuevoMaster.ParentModelId = ParentSeleccionado?.Id ?? origen.ParentModelId;
        NuevoMaster.ModelQrPartNumber = origen.ModelQrPartNumber;
        NuevoMaster.ModelProductId = origen.ModelProductId;
        NuevoMaster.PcbsPerPanel = origen.PcbsPerPanel;
        NuevoMaster.UiColor = origen.UiColor;
        NuevoMaster.SimulateInterlock = origen.SimulateInterlock;
        NuevoMaster.ValidateInterlock = origen.ValidateInterlock;
        NuevoMaster.SerialNumberByMes = origen.SerialNumberByMes;
        NuevoMaster.SerialNumberCreationSp = origen.SerialNumberCreationSp;
        NuevoMaster.ForceSecondarySerialNumber = origen.ForceSecondarySerialNumber;
        NuevoMaster.ModelPartNumber2 = origen.ModelPartNumber2;
        NuevoMaster.FgPartNumber = origen.FgPartNumber;
        NuevoMaster.HandlingLogic = origen.HandlingLogic;
        NuevoMaster.IsActive = true;
    }

    public void EmpezarDesdeCero()
    {
        MasterBase = null;
        NuevoMaster.ModelPartNumber = NumeroDeParteBuscado;
        NuevoMaster.ParentModelId = ParentSeleccionado?.Id;
    }

    public async Task<bool> GuardarMasterAsync()
    {
        if (_service is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ResponsableMaster))
        {
            ErrorMessage = "Elige quién hizo este paso antes de guardar el modelo.";
            return false;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var id = await _service.CrearMasterAsync(NuevoMaster);
            NuevoMaster.Id = id;
            MasterCreado = NuevoMaster;

            // El área ya viene como "SMT-BE" combinada cuando aplica (ver
            // ModelRecord.AreasDisponibles) — se marca la misma área que
            // se está viendo, sin traducir a "SMT"/"BE" por separado.
            await MarcarEnSeguimientoLocalAsync(ResponsableMaster, d => d.ImesModelsMaster = ResponsableMaster!);
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.GuardarMasterAsync", ex);
            ErrorMessage = $"No se pudo guardar: {LogService.Redactar(ex.Message)}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---- Paso 3 ----

    // Si el Master se basó en uno existente, trae los segmentos de
    // serial de ESE modelo (no del nuevo) para poder copiarlos.
    public async Task CargarSpecsDelModeloBaseAsync()
    {
        if (_service is null || MasterBase is null)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var specs = await _service.ObtenerSerialSpecsDeModeloAsync(MasterBase.Id);
            SpecsDelModeloBase.Clear();
            foreach (var spec in specs)
            {
                SpecsDelModeloBase.Add(spec);
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.CargarSpecsDelModeloBaseAsync", ex);
            ErrorMessage = $"No se pudieron cargar los segmentos del modelo base: {LogService.Redactar(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Si el Master ya existía (encontrado por VerificarMasterExistenteAsync
    // o recién guardado), revisa si ya tiene segmentos de serial
    // capturados — para no tener que armarlos de nuevo si ya están.
    public async Task<List<ImesSerialNumberSpec>> VerificarSpecsExistentesAsync()
    {
        if (_service is null || MasterCreado is null)
        {
            return new List<ImesSerialNumberSpec>();
        }

        try
        {
            var specs = await _service.ObtenerSerialSpecsDeModeloAsync(MasterCreado.Id);
            if (specs.Count > 0)
            {
                await MarcarDescubiertoEnSeguimientoLocalAsync(
                    d => ConfigStatusHelper.IsCompleted(d.ImesModelsSerialNumberSpecs),
                    d =>
                    {
                        d.ImesModelsSerialNumberSpecs = ValorParaMarcarDescubierto(ResponsableSpecs);
                        d.ImesModelsSerialNumberSpecsPacking = ValorParaMarcarDescubierto(ResponsableSpecs);
                    });
            }

            return specs;
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.VerificarSpecsExistentesAsync", ex);
            return new List<ImesSerialNumberSpec>();
        }
    }

    // Saca del PDF adjunto del modelo los candidatos a "serial de
    // ejemplo" — solo llena el campo de arriba, no lo prueba ni arranca
    // la segmentación solo; eso lo decide el usuario con sus propios
    // botones ("Empezar" aquí, "Probar" en la otra tarjeta).
    public Task<List<SerialCandidato>> ExtraerCandidatosSerialDelPdfAsync()
    {
        if (Modelo is null || !Modelo.TieneDocumentoAdjunto)
        {
            ErrorMessage = "Este modelo no tiene un PDF adjunto todavía (usa \"Adjuntar PDF\" en Altas de Modelos).";
            return Task.FromResult(new List<SerialCandidato>());
        }

        var rutaPdf = DocumentAttachmentService.ObtenerRutaCompleta(_settings.DatabasePath, Modelo.DocumentoAdjunto);
        var candidatos = PdfSerialExtractorService.ExtraerCandidatosSerial(rutaPdf);

        if (candidatos.Count == 0)
        {
            ErrorMessage = "No se encontró un \"Laser Serial Number real example\" en el PDF adjunto — cópialo a mano.";
        }

        return Task.FromResult(candidatos);
    }

    public void CopiarSpecsDelModeloBase()
    {
        SpecsNuevos.Clear();
        foreach (var spec in SpecsDelModeloBase)
        {
            SpecsNuevos.Add(new ImesSerialNumberSpec
            {
                ValueDescription = spec.ValueDescription,
                StartPosition = spec.StartPosition,
                ExpectedCharacterLen = spec.ExpectedCharacterLen,
                IsDynamicValue = spec.IsDynamicValue,
                FixedCharacterValue = spec.FixedCharacterValue
            });
        }
    }

    // ---- Armado visual: clic secuencial sobre un serial de ejemplo ----

    // Etiquetas ya vistas en otros modelos, para sugerir en el
    // desplegable — pero es editable: si el serial trae algo que no está
    // aquí, se escribe el nombre que haga falta.
    public string[] EtiquetasComunes { get; } = new[]
    {
        "ClientPartNumber", "Separator", "Revision", "BuildDate",
        "ProductIdentifier", "UniqueId", "PartNumberIndicator", "QualityLvl", "BuildSiteCode"
    };

    private static readonly Windows.UI.Color[] PaletaColores =
    {
        Windows.UI.Color.FromArgb(255, 219, 234, 254),
        Windows.UI.Color.FromArgb(255, 220, 252, 231),
        Windows.UI.Color.FromArgb(255, 254, 240, 199),
        Windows.UI.Color.FromArgb(255, 250, 224, 233),
        Windows.UI.Color.FromArgb(255, 224, 231, 255),
        Windows.UI.Color.FromArgb(255, 209, 250, 229)
    };

    [ObservableProperty]
    private string serialEjemplo = string.Empty;

    public ObservableCollection<SerialCharacterCell> Celdas { get; } = new();

    // Posición (1-indexada) donde empezaría el siguiente segmento.
    private int _cursorPosicion = 1;

    public bool SegmentacionCompleta => Celdas.Count > 0 && _cursorPosicion > SerialEjemplo.Length;

    // Arranca (o reinicia) el armado visual con el serial que se haya escrito.
    public void IniciarSegmentacion()
    {
        SpecsNuevos.Clear();
        _cursorPosicion = 1;
        ReconstruirCeldas();
    }

    private void ReconstruirCeldas()
    {
        Celdas.Clear();
        for (var i = 0; i < SerialEjemplo.Length; i++)
        {
            var posicion = i + 1;
            var indiceSegmento = -1;
            for (var s = 0; s < SpecsNuevos.Count; s++)
            {
                var spec = SpecsNuevos[s];
                if (posicion >= spec.StartPosition && posicion < spec.StartPosition + spec.ExpectedCharacterLen)
                {
                    indiceSegmento = s;
                    break;
                }
            }

            var color = indiceSegmento >= 0 ? PaletaColores[indiceSegmento % PaletaColores.Length] : Windows.UI.Color.FromArgb(0, 0, 0, 0);

            Celdas.Add(new SerialCharacterCell
            {
                Character = SerialEjemplo[i],
                Position = posicion,
                IndiceSegmento = indiceSegmento,
                Fondo = new Microsoft.UI.Xaml.Media.SolidColorBrush(color)
            });
        }
    }

    // El pedazo del serial que se cerraría si el usuario confirma hasta
    // esta posición — para mostrarlo en el mini formulario antes de guardar.
    public (int Start, int Length, string Valor)? ObtenerSegmentoPendiente(int posicionFin)
    {
        if (posicionFin < _cursorPosicion || posicionFin > SerialEjemplo.Length)
        {
            return null;
        }

        var start = _cursorPosicion;
        var length = posicionFin - _cursorPosicion + 1;
        var valor = SerialEjemplo.Substring(start - 1, length);
        return (start, length, valor);
    }

    public void ConfirmarSegmento(int posicionFin, string etiqueta, bool esDinamico)
    {
        var pendiente = ObtenerSegmentoPendiente(posicionFin);
        if (pendiente is not { } segmento || string.IsNullOrWhiteSpace(etiqueta))
        {
            return;
        }

        SpecsNuevos.Add(new ImesSerialNumberSpec
        {
            ValueDescription = etiqueta.Trim(),
            StartPosition = segmento.Start,
            ExpectedCharacterLen = segmento.Length,
            IsDynamicValue = esDinamico,
            FixedCharacterValue = esDinamico ? string.Empty : segmento.Valor
        });

        _cursorPosicion = posicionFin + 1;
        ReconstruirCeldas();
        OnPropertyChanged(nameof(SegmentacionCompleta));
    }

    // Deshace el último segmento confirmado, por si se marcó algo mal.
    public void QuitarUltimoSegmento()
    {
        if (SpecsNuevos.Count == 0)
        {
            return;
        }

        var ultimo = SpecsNuevos[^1];
        SpecsNuevos.RemoveAt(SpecsNuevos.Count - 1);
        _cursorPosicion = ultimo.StartPosition;
        ReconstruirCeldas();
        OnPropertyChanged(nameof(SegmentacionCompleta));
    }

    public async Task<bool> GuardarSpecsAsync()
    {
        if (_service is null)
        {
            return false;
        }

        if (MasterCreado is null)
        {
            ErrorMessage = "Primero guarda el modelo en el Paso 2 (MODELS_MASTER) — los segmentos se ligan a ese modelo.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ResponsableSpecs))
        {
            ErrorMessage = "Elige quién hizo este paso antes de guardar los segmentos.";
            return false;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _service.CrearSerialSpecsAsync(MasterCreado.Id, SpecsNuevos);
            await MarcarEnSeguimientoLocalAsync(ResponsableSpecs, d =>
            {
                d.ImesModelsSerialNumberSpecs = ResponsableSpecs!;
                d.ImesModelsSerialNumberSpecsPacking = ResponsableSpecs!;
            });
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("ConfigurarEnImesViewModel.GuardarSpecsAsync", ex);
            ErrorMessage = $"No se pudieron guardar los segmentos: {LogService.Redactar(ex.Message)}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
