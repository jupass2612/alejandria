using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Services;

namespace Alejandria.ViewModels;

// ViewModel de "Historial de Modelos" — solo lectura de las 3 tablas de
// iMES, para revisar qué ya hay registrado sin poder editar nada desde
// aquí (eso sigue siendo "Configurar en iMES").
public partial class HistorialModelosImesViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();
    private ImesOracleModelService? _service;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    [ObservableProperty]
    private bool isBusy;

    public ObservableCollection<ImesParentModel> Parents { get; } = new();
    public ObservableCollection<ImesMasterModel> Masters { get; } = new();
    public ObservableCollection<ImesSerialNumberSpec> SerialSpecs { get; } = new();

    public string EntornoActivoTexto => _settings.ImesOracleEntornoActivo;

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

    public async Task CargarParentsAsync()
    {
        if (_service is null)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var parents = await _service.ObtenerTodosLosParentsAsync();
            Parents.Clear();
            foreach (var parent in parents)
            {
                Parents.Add(parent);
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("HistorialModelosImesViewModel.CargarParentsAsync", ex);
            ErrorMessage = $"No se pudo cargar: {LogService.Redactar(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CargarMastersAsync()
    {
        if (_service is null)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var masters = await _service.ObtenerTodosLosMastersAsync();
            Masters.Clear();
            foreach (var master in masters)
            {
                Masters.Add(master);
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("HistorialModelosImesViewModel.CargarMastersAsync", ex);
            ErrorMessage = $"No se pudo cargar: {LogService.Redactar(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task CargarSerialSpecsAsync()
    {
        if (_service is null)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var specs = await _service.ObtenerTodosLosSerialSpecsAsync();
            SerialSpecs.Clear();
            foreach (var spec in specs)
            {
                SerialSpecs.Add(spec);
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("HistorialModelosImesViewModel.CargarSerialSpecsAsync", ex);
            ErrorMessage = $"No se pudo cargar: {LogService.Redactar(ex.Message)}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
