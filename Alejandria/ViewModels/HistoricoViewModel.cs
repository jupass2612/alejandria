using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Services;

namespace Alejandria.ViewModels;

// ViewModel de "Histórico": modelos con Estatus General Terminado. Es de
// solo lectura. El buscador filtra en tiempo real, con cada tecla.
public partial class HistoricoViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();

    // Todos los modelos en Histórico, sin filtrar.
    private List<ModeloResumenItem> _todos = new();

    public ObservableCollection<ModeloResumenItem> Modelos { get; } = new();

    [ObservableProperty]
    private string filtroTexto = string.Empty;

    partial void OnFiltroTextoChanged(string value)
    {
        AplicarFiltro();
        OnPropertyChanged(nameof(MensajeVacio));
    }

    public string MensajeVacio => string.IsNullOrWhiteSpace(FiltroTexto)
        ? "Todavía no hay modelos en Histórico. Aparecen aquí automáticamente cuando su Estatus General llega a Terminado."
        : $"Ningún modelo en Histórico coincide con \"{FiltroTexto.Trim()}\".";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
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

    // Filtra por nombre o número de parte, sin distinguir mayúsculas/minúsculas.
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

    // Crea el modelo de actualización. Las áreas que NO cambiaron copian su
    // config del original; las que sí cambiaron quedan en blanco a propósito.
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
                    PartLineBomMaintenance = detalleOriginal.PartLineBomMaintenance
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

    // Adjunta el PDF que justifica el cambio, mismo mecanismo que "Adjuntar PDF".
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

    // Para "Ver detalles" — la configuración ya guardada de cada área,
    // de solo lectura. No incluye Unity (se quitó ese apartado).
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

    // Reemplaza el PDF de un modelo en Histórico cuyo documento quedó
    // marcado "Pendiente a correcciones" — mismo mecanismo que adjuntar
    // en Altas de Modelos, sin tener que pasar por "Actualizar".
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

            // Este botón solo aparece cuando el documento estaba pendiente
            // de corrección — adjuntar aquí ES la corrección, así que se
            // regresa solo a "Correcto" en las áreas que lo tenían pendiente.
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
