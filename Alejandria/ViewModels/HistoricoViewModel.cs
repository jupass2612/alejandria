using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Services;

namespace Alejandria.ViewModels;

// Histórico: modelos terminados, solo lectura, buscador en vivo
public partial class HistoricoViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();

    // Todos sin filtro
    private List<ModeloResumenItem> _todos = new();

    public ObservableCollection<ModeloResumenItem> Modelos { get; } = new();

    [ObservableProperty]
    private string filtroTexto = string.Empty;

    partial void OnFiltroTextoChanged(string value)
    {
        AplicarFiltro();
        OnPropertyChanged(nameof(TituloVacio));
        OnPropertyChanged(nameof(MensajeVacio));
        OnPropertyChanged(nameof(MostrarEstadoVacio));
    }

    public string TituloVacio => string.IsNullOrWhiteSpace(FiltroTexto)
        ? "Todavía no hay modelos en Histórico"
        : "Ningún modelo coincide";

    public string MensajeVacio => string.IsNullOrWhiteSpace(FiltroTexto)
        ? "Aparecen aquí solos cuando todas sus áreas llegan a Terminado."
        : $"Nada en Histórico coincide con \"{FiltroTexto.Trim()}\".";

    // Cargando o lista vacía
    public bool MostrarEstadoVacio => IsLoading || (!HasError && Modelos.Count == 0);

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(MostrarEstadoVacio));

    public HistoricoViewModel()
    {
        Modelos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(MostrarEstadoVacio));
    }

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(MostrarEstadoVacio));
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _settings.Load();
            var database = new ModelDatabaseService(_settings.DatabasePath);
            await database.InitializeAsync();

            var (modelos, detalles) = await database.GetModelosConDetallesAsync();

            var historicos = new List<ModeloResumenItem>();
            foreach (var modelo in modelos)
            {
                detalles.TryGetValue(modelo.Id, out var detallesDelModelo);
                var resumen = ConfigStatusHelper.ComputeResumenModelo(modelo, detallesDelModelo);

                if (resumen.EstatusGeneral != ConfigStatusHelper.Terminado)
                {
                    continue;
                }

                historicos.Add(new ModeloResumenItem
                {
                    Modelo = modelo,
                    EstatusBd = resumen.EstatusBd,
                    EstatusPlataformaIMES = resumen.EstatusPlataforma,
                    EstatusGeneral = resumen.EstatusGeneral,
                    FechaLimite = resumen.FechaLimite,
                    DiasRetraso = resumen.DiasRetraso,
                    NecesitaCorreccionDocumento = ConfigStatusHelper.TieneDocumentoPendienteCorreccion(detallesDelModelo)
                });
            }

            _todos = historicos;
            AplicarFiltro();
        }
        catch (Exception ex)
        {
            LogService.LogError("HistoricoViewModel.InitializeAsync", ex);
            ErrorMessage = "No se pudo conectar a la base de datos compartida — revisa tu conexión de red o VPN de la empresa. Más detalles en el registro de errores (Ajustes).";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Filtra por nombre o número de parte
    private void AplicarFiltro()
    {
        var texto = FiltroTexto?.Trim() ?? string.Empty;

        Modelos.Clear();

        var coincidencias = string.IsNullOrEmpty(texto)
            ? _todos
            : _todos.Where(item => Coincide(item.Modelo, texto));

        foreach (var item in coincidencias)
        {
            Modelos.Add(item);
        }
    }

    private static bool Coincide(ModelRecord modelo, string texto) =>
        Contiene(modelo.NombreModelo, texto) ||
        Contiene(modelo.NumeroParteSmt, texto) ||
        Contiene(modelo.NumeroParteBe, texto) ||
        Contiene(modelo.NumeroParteTla, texto);

    private static bool Contiene(string campo, string texto) =>
        !string.IsNullOrEmpty(campo) && campo.Contains(texto, StringComparison.OrdinalIgnoreCase);

    // Crea la actualización: las áreas que no cambiaron copian su config, las que sí quedan en blanco
    public async Task<ModelRecord?> CrearActualizacionAsync(ModelRecord original, ModelRecord nuevo, IReadOnlySet<string> areasCambiadas)
    {
        ErrorMessage = null;

        try
        {
            _settings.Load();
            var database = new ModelDatabaseService(_settings.DatabasePath);
            await database.InitializeAsync();

            var nuevoId = await database.InsertAsync(nuevo);
            nuevo.Id = nuevoId;

            foreach (var area in nuevo.AreasDisponibles)
            {
                if (areasCambiadas.Contains(area))
                {
                    continue;
                }

                var detalleOriginal = await database.GetDetalleAsync(original.Id, area);
                if (detalleOriginal is null)
                {
                    continue;
                }

                await database.SaveDetalleAsync(new DetalleConfiguracion
                {
                    ModeloId = nuevoId,
                    Area = area,
                    ImesModelsParents = detalleOriginal.ImesModelsParents,
                    ImesModelsMaster = detalleOriginal.ImesModelsMaster,
                    ImesModelsSerialNumberSpecs = detalleOriginal.ImesModelsSerialNumberSpecs,
                    ImesModelsSerialNumberSpecsPacking = detalleOriginal.ImesModelsSerialNumberSpecsPacking,
                    WorkOrder = detalleOriginal.WorkOrder,
                    Flujo = detalleOriginal.Flujo,
                    Ruta = detalleOriginal.Ruta,
                    Linea = detalleOriginal.Linea,
                    WorkOrderBe = detalleOriginal.WorkOrderBe,
                    LineaBe = detalleOriginal.LineaBe,
                    PartLineBomMaintenance = detalleOriginal.PartLineBomMaintenance,
                    ImesModelId = detalleOriginal.ImesModelId
                });
            }

            return nuevo;
        }
        catch (Exception ex)
        {
            LogService.LogError("HistoricoViewModel.CrearActualizacionAsync", ex);
            ErrorMessage = $"No se pudo crear la actualización: {LogService.Redactar(ex.Message)}";
            return null;
        }
    }

    // PDF que justifica el cambio
    public async Task<bool> AdjuntarJustificacionAsync(ModelRecord nuevo, string archivoOrigen)
    {
        try
        {
            _settings.Load();
            var database = new ModelDatabaseService(_settings.DatabasePath);
            await database.InitializeAsync();

            var nombreGuardado = await DocumentAttachmentService.AdjuntarAsync(_settings.DatabasePath, nuevo.Id, archivoOrigen);
            await database.UpdateDocumentoAdjuntoAsync(nuevo.Id, nombreGuardado);
            nuevo.DocumentoAdjunto = nombreGuardado;
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("HistoricoViewModel.AdjuntarJustificacionAsync", ex);
            ErrorMessage = $"El modelo se creó, pero no se pudo adjuntar el PDF de justificación: {LogService.Redactar(ex.Message)}";
            return false;
        }
    }

    // Config guardada de cada área para Ver detalles
    public async Task<List<DetalleConfiguracion>> ObtenerDetallesAsync(ModelRecord modelo)
    {
        try
        {
            _settings.Load();
            var database = new ModelDatabaseService(_settings.DatabasePath);
            await database.InitializeAsync();
            return await database.GetDetallesDeModeloAsync(modelo.Id);
        }
        catch (Exception ex)
        {
            LogService.LogError("HistoricoViewModel.ObtenerDetallesAsync", ex);
            ErrorMessage = $"No se pudo cargar la configuración: {LogService.Redactar(ex.Message)}";
            return new List<DetalleConfiguracion>();
        }
    }

    // Reemplaza el PDF de un modelo en Histórico con documento pendiente
    public async Task<bool> AdjuntarPdfCorregidoAsync(ModelRecord modelo, string archivoOrigen)
    {
        try
        {
            _settings.Load();
            var database = new ModelDatabaseService(_settings.DatabasePath);
            await database.InitializeAsync();

            var nombreGuardado = await DocumentAttachmentService.AdjuntarAsync(_settings.DatabasePath, modelo.Id, archivoOrigen);
            await database.UpdateDocumentoAdjuntoAsync(modelo.Id, nombreGuardado);
            modelo.DocumentoAdjunto = nombreGuardado;

            // Adjuntar aquí es la corrección, se regresa a Correcto
            var detalles = await database.GetDetallesDeModeloAsync(modelo.Id);
            foreach (var detalle in detalles.Where(d => d.EstadoDocumento == ConfigStatusHelper.DocumentoPendienteCorrecciones))
            {
                detalle.EstadoDocumento = ConfigStatusHelper.DocumentoCorrecto;
                await database.SaveDetalleAsync(detalle);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("HistoricoViewModel.AdjuntarPdfCorregidoAsync", ex);
            ErrorMessage = $"No se pudo adjuntar el PDF corregido: {LogService.Redactar(ex.Message)}";
            return false;
        }
    }
}
