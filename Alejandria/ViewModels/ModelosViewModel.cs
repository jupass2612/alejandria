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

    // Avance por paso (1 Parents, 2 Master, 3 SerialSpecs, 4 Plataforma)
    public string EstatusPaso1 { get; init; } = ConfigStatusHelper.Pendiente;
    public string EstatusPaso2 { get; init; } = ConfigStatusHelper.Pendiente;
    public string EstatusPaso3 { get; init; } = ConfigStatusHelper.Pendiente;
    public string EstatusPaso4 { get; init; } = ConfigStatusHelper.Pendiente;

    public int PasosTerminados =>
        new[] { EstatusPaso1, EstatusPaso2, EstatusPaso3, EstatusPaso4 }.Count(e => e == ConfigStatusHelper.Terminado);

    public string AvanceTexto => $"{PasosTerminados}/4 pasos";

    public string AvanceTooltip =>
        $"1 Parents: {EstatusPaso1} · 2 Master: {EstatusPaso2} · 3 SerialSpecs: {EstatusPaso3} · 4 Plataforma: {EstatusPaso4}";
}

// ViewModel de "Modelos": lista los modelos con el avance de su configuración.
public partial class ModelosViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();

    public ModelDatabaseService? Database { get; private set; }

    // Lo que se muestra (ya filtrado y ordenado)
    public ObservableCollection<ModeloResumenItem> Modelos { get; } = new();

    // Todos los modelos pendientes, sin filtro
    private readonly List<ModeloResumenItem> _todos = new();

    public const string FiltroTodos = "Todos";
    public const string FiltroPendientes = "Pendientes";
    public const string FiltroEnProceso = "En proceso";
    public const string FiltroAtrasados = "Atrasados";

    [ObservableProperty]
    private string busqueda = string.Empty;

    partial void OnBusquedaChanged(string value) => AplicarFiltro();

    [ObservableProperty]
    private string filtro = FiltroTodos;

    partial void OnFiltroChanged(string value) => AplicarFiltro();

    [ObservableProperty]
    private string conteoTexto = string.Empty;

    // Hay modelos pero el filtro no dejó ninguno
    public bool SinResultados => _todos.Count > 0 && Modelos.Count == 0;

    // No hay ningún modelo pendiente
    public bool SinModelos => !IsLoading && !HasError && _todos.Count == 0;

    // Se muestra mientras carga o cuando no hay ningún modelo pendiente
    public bool MostrarEstadoVacio => IsLoading || SinModelos;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(SinModelos));
        OnPropertyChanged(nameof(MostrarEstadoVacio));
    }

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(SinModelos));
        OnPropertyChanged(nameof(MostrarEstadoVacio));
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

        _todos.Clear();
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

            var pasos = ConfigStatusHelper.EstatusPasosModelo(modelo, detallesDelModelo);

            _todos.Add(new ModeloResumenItem
            {
                Modelo = modelo,
                EstatusBd = resumen.EstatusBd,
                EstatusPlataformaIMES = resumen.EstatusPlataforma,
                EstatusGeneral = resumen.EstatusGeneral,
                FechaLimite = resumen.FechaLimite,
                DiasRetraso = resumen.DiasRetraso,
                EstatusPaso1 = pasos[0],
                EstatusPaso2 = pasos[1],
                EstatusPaso3 = pasos[2],
                EstatusPaso4 = pasos[3]
            });
        }

        AplicarFiltro();
    }

    // Busca por nombre, número de modelo o número de parte, filtra por chip
    // y deja los atrasados arriba (los más atrasados primero)
    private void AplicarFiltro()
    {
        var texto = Busqueda.Trim();

        var filtrados = _todos.Where(m =>
        {
            var coincide = texto.Length == 0
                || m.Modelo.NombreModelo.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || m.Modelo.NumeroModelo.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || m.Modelo.NumeroParteSmt.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || m.Modelo.NumeroParteBe.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || m.Modelo.NumeroParteTla.Contains(texto, StringComparison.OrdinalIgnoreCase);

            if (!coincide)
            {
                return false;
            }

            return Filtro switch
            {
                FiltroPendientes => m.EstatusGeneral == ConfigStatusHelper.Pendiente,
                FiltroEnProceso => m.EstatusGeneral == ConfigStatusHelper.EnProceso,
                FiltroAtrasados => m.EstaAtrasado,
                _ => true
            };
        })
        .OrderByDescending(m => m.DiasRetraso)
        .ThenBy(m => m.FechaLimite)
        .ToList();

        Modelos.Clear();
        foreach (var item in filtrados)
        {
            Modelos.Add(item);
        }

        var atrasados = _todos.Count(m => m.EstaAtrasado);
        ConteoTexto = filtrados.Count == _todos.Count
            ? $"{_todos.Count} modelo{(_todos.Count == 1 ? string.Empty : "s")} pendiente{(_todos.Count == 1 ? string.Empty : "s")} · {atrasados} atrasado{(atrasados == 1 ? string.Empty : "s")}"
            : $"{filtrados.Count} de {_todos.Count}";

        OnPropertyChanged(nameof(SinResultados));
        OnPropertyChanged(nameof(SinModelos));
        OnPropertyChanged(nameof(MostrarEstadoVacio));
    }

    public void QuitarFiltros()
    {
        Busqueda = string.Empty;
        Filtro = FiltroTodos;
    }
}
