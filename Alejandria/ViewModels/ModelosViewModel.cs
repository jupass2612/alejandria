using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Services;

namespace Alejandria.ViewModels;

// Fila de la tabla de Modelos: el registro más sus estatus calculados.
public class ModeloResumenItem
{
    public required ModelRecord Modelo { get; init; }
    public required string EstatusBd { get; init; }
    public required string EstatusPlataformaIMES { get; init; }
    public required string EstatusGeneral { get; init; }
    public required DateTime FechaLimite { get; init; }
    public required int DiasRetraso { get; init; }

    public string FechaLimiteTexto => FechaLimite.ToString("dd/MM/yyyy");
    public bool EstaAtrasado => DiasRetraso > 0;
    public string RetrasoTexto => EstaAtrasado
        ? $"{DiasRetraso} día{(DiasRetraso == 1 ? string.Empty : "s")} de retraso"
        : "En plazo";

    // True si alguna área tiene el documento marcado "Pendiente a
    // correcciones" — en Histórico, esto habilita "Adjuntar PDF" para
    // poder subir la versión corregida sin usar "Actualizar".
    public bool NecesitaCorreccionDocumento { get; init; }
}

// ViewModel de "Modelos": lista los modelos con el avance de su configuración.
public partial class ModelosViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();

    public ModelDatabaseService? Database { get; private set; }

    public ObservableCollection<ModeloResumenItem> Modelos { get; } = new();

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

            var directory = Path.GetDirectoryName(_settings.DatabasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Database = new ModelDatabaseService(_settings.DatabasePath);
            await Database.InitializeAsync();
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            LogService.LogError("ModelosViewModel.InitializeAsync", ex);
            ErrorMessage = "No se pudo conectar a la base de datos compartida — revisa tu conexión de red o VPN de la empresa. Más detalles en el registro de errores (Ajustes).";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ReloadAsync()
    {
        if (Database is null)
        {
            return;
        }

        var (modelos, detalles) = await Database.GetModelosConDetallesAsync();

        Modelos.Clear();
        foreach (var modelo in modelos)
        {
            detalles.TryGetValue(modelo.Id, out var detallesDelModelo);
            var resumen = ConfigStatusHelper.ComputeResumenModelo(modelo, detallesDelModelo);

            // Los modelos ya Terminados (todas sus áreas) "pasan" a Histórico
            // y dejan de aparecer aquí — ver HistoricoViewModel.
            if (resumen.EstatusGeneral == ConfigStatusHelper.Terminado)
            {
                continue;
            }

            Modelos.Add(new ModeloResumenItem
            {
                Modelo = modelo,
                EstatusBd = resumen.EstatusBd,
                EstatusPlataformaIMES = resumen.EstatusPlataforma,
                EstatusGeneral = resumen.EstatusGeneral,
                FechaLimite = resumen.FechaLimite,
                DiasRetraso = resumen.DiasRetraso
            });
        }
    }
}
