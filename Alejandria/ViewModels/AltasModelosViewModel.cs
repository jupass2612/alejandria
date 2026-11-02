using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Services;

namespace Alejandria.ViewModels;

// ViewModel de la pantalla principal: la tabla de seguimiento de altas de modelos.
public partial class AltasModelosViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();
    private ModelDatabaseService? _database;

    public ObservableCollection<ModelRecord> Records { get; } = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    // Se recalcula cuando ErrorMessage cambia.
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(MostrarEstadoVacio));
    }

    // Cargando, o sin altas todavía
    public bool MostrarEstadoVacio => IsLoading || (!HasError && Records.Count == 0);

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(MostrarEstadoVacio));

    public AltasModelosViewModel()
    {
        Records.CollectionChanged += (_, _) => OnPropertyChanged(nameof(MostrarEstadoVacio));
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _settings.Load();

            var directory = Path.GetDirectoryName(_settings.DatabasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _database = new ModelDatabaseService(_settings.DatabasePath);
            await _database.InitializeAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            LogService.LogError("AltasModelosViewModel.InitializeAsync", ex);
            ErrorMessage = "No se pudo conectar a la base de datos compartida — revisa tu conexión de red o VPN de la empresa. Más detalles en el registro de errores (Ajustes).";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReloadAsync()
    {
        if (_database is null)
        {
            return;
        }

        var (modelos, detalles) = await _database.GetModelosConDetallesAsync();

        Records.Clear();
        foreach (var modelo in modelos)
        {
            detalles.TryGetValue(modelo.Id, out var detallesDelModelo);
            var resumen = ConfigStatusHelper.ComputeResumenModelo(modelo, detallesDelModelo);
            modelo.EsHistorico = resumen.EstatusGeneral == ConfigStatusHelper.Terminado;

            Records.Add(modelo);
        }
    }

    public async Task<bool> AddModelAsync(ModelRecord record)
    {
        if (_database is null)
        {
            ErrorMessage = "La base de datos no está disponible todavía.";
            return false;
        }

        try
        {
            record.Id = await _database.InsertAsync(record);
            Records.Insert(0, record);
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("AltasModelosViewModel.AddModelAsync", ex);
            ErrorMessage = $"No se pudo guardar el registro: {LogService.Redactar(ex.Message)}";
            return false;
        }
    }

    public async Task<bool> UpdateModelAsync(ModelRecord record)
    {
        if (_database is null)
        {
            ErrorMessage = "La base de datos no está disponible todavía.";
            return false;
        }

        if (record.EsHistorico)
        {
            ErrorMessage = "Este modelo ya está en Histórico (Estatus General Terminado) y no se puede editar.";
            return false;
        }

        try
        {
            await _database.UpdateAsync(record);
            await ReloadAsync();
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("AltasModelosViewModel.UpdateModelAsync", ex);
            ErrorMessage = $"No se pudo actualizar el registro: {LogService.Redactar(ex.Message)}";
            return false;
        }
    }

    // Copia el PDF a la carpeta compartida y guarda la referencia en el modelo.
    public async Task<bool> AdjuntarDocumentoAsync(ModelRecord record, string archivoOrigen)
    {
        if (_database is null)
        {
            ErrorMessage = "La base de datos no está disponible todavía.";
            return false;
        }

        if (record.EsHistorico)
        {
            ErrorMessage = "Este modelo ya está en Histórico (Estatus General Terminado) y no se puede editar.";
            return false;
        }

        try
        {
            var nombreGuardado = await DocumentAttachmentService.AdjuntarAsync(_settings.DatabasePath, record.Id, archivoOrigen);
            await _database.UpdateDocumentoAdjuntoAsync(record.Id, nombreGuardado);
            await ReloadAsync();
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("AltasModelosViewModel.AdjuntarDocumentoAsync", ex);
            ErrorMessage = $"No se pudo adjuntar el documento: {LogService.Redactar(ex.Message)}";
            return false;
        }
    }

    // Áreas de este modelo cuyo documento sigue marcado "Pendiente a
    // correcciones" — para preguntar si ya se resolvió al adjuntar un PDF nuevo.
    public async Task<List<string>> ObtenerAreasPendientesDeCorreccionAsync(int modeloId)
    {
        if (_database is null)
        {
            return new List<string>();
        }

        var detalles = await _database.GetDetallesDeModeloAsync(modeloId);
        return detalles
            .Where(d => d.EstadoDocumento == ConfigStatusHelper.DocumentoPendienteCorrecciones)
            .Select(d => d.Area)
            .ToList();
    }

    // Regresa a "Correcto" el Estado del Documento de las áreas indicadas.
    public async Task ResetearEstadoDocumentoAsync(int modeloId, IEnumerable<string> areas)
    {
        if (_database is null)
        {
            return;
        }

        foreach (var area in areas)
        {
            var detalle = await _database.GetDetalleAsync(modeloId, area);
            if (detalle is null)
            {
                continue;
            }

            detalle.EstadoDocumento = ConfigStatusHelper.DocumentoCorrecto;
            await _database.SaveDetalleAsync(detalle);
        }
    }

    // Abre el PDF adjunto con el visor predeterminado de Windows.
    public void AbrirDocumento(ModelRecord record)
    {
        try
        {
            DocumentAttachmentService.Abrir(_settings.DatabasePath, record.DocumentoAdjunto);
        }
        catch (Exception ex)
        {
            LogService.LogError("AltasModelosViewModel.AbrirDocumento", ex);
            ErrorMessage = $"No se pudo abrir el documento: {LogService.Redactar(ex.Message)}";
        }
    }
}
