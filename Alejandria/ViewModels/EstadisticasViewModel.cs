using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Services;

namespace Alejandria.ViewModels;

// Tarjeta de KPI
public class KpiItem
{
    public required string Label { get; init; }
    public required string Value { get; init; }

    // Success, Warning, Pendiente, Danger o null
    public string? Tono { get; init; }

    public bool TieneTono => !string.IsNullOrEmpty(Tono);
}

// Configuraciones por responsable, con barra proporcional
public class ResponsableResumenItem
{
    public required string Nombre { get; init; }
    public required int TareasCompletadas { get; init; }

    // 0 a 1 contra el que más tiene
    public required double Proporcion { get; init; }
}

// Estadísticas: KPIs, responsables y reportes.
// Los KPIs cuentan todo, los reportes solo Histórico
public partial class EstadisticasViewModel : ObservableObject
{
    private readonly AppSettingsService _settings = new();

    // Se guardan para no volver a consultar al exportar
    private Dictionary<int, List<DetalleConfiguracion>> _detallesPorModelo = new();
    private List<ModeloResumenItem> _modelosResumen = new();

    // Solo Histórico, es lo que entra en los reportes
    private List<ModelRecord> _modelosHistorico = new();

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    [ObservableProperty]
    private bool isExportingExcel;

    [ObservableProperty]
    private bool isExportingPdf;

    [ObservableProperty]
    private string? reportMessage;

    // Último reporte, para Abrir reporte
    [ObservableProperty]
    private string? ultimoReportePath;

    public bool TieneReporteGenerado => !string.IsNullOrEmpty(UltimoReportePath);

    partial void OnUltimoReportePathChanged(string? value) => OnPropertyChanged(nameof(TieneReporteGenerado));

    public ObservableCollection<KpiItem> Kpis { get; } = new();
    public ObservableCollection<ResponsableResumenItem> Responsables { get; } = new();

    // Selector de modelos del reporte especializado

    [ObservableProperty]
    private string busquedaModelo = string.Empty;

    partial void OnBusquedaModeloChanged(string value) => AplicarFiltroBusqueda();

    // Coinciden con la búsqueda y no están elegidos
    public ObservableCollection<ModelRecord> SugerenciasBusqueda { get; } = new();

    // Modelos ya agregados al reporte
    public ObservableCollection<ModelRecord> ModelosSeleccionados { get; } = new();

    public void AgregarModeloSeleccionado(ModelRecord modelo)
    {
        if (ModelosSeleccionados.All(m => m.Id != modelo.Id))
        {
            ModelosSeleccionados.Add(modelo);
        }

        AplicarFiltroBusqueda();
    }

    public void QuitarModeloSeleccionado(ModelRecord modelo)
    {
        var existente = ModelosSeleccionados.FirstOrDefault(m => m.Id == modelo.Id);
        if (existente is not null)
        {
            ModelosSeleccionados.Remove(existente);
        }

        AplicarFiltroBusqueda();
    }

    // Filtra por nombre o número de parte; vacío no muestra nada
    private void AplicarFiltroBusqueda()
    {
        SugerenciasBusqueda.Clear();

        var texto = BusquedaModelo?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(texto))
        {
            return;
        }

        var yaElegidos = ModelosSeleccionados.Select(m => m.Id).ToHashSet();

        foreach (var modelo in _modelosHistorico)
        {
            if (yaElegidos.Contains(modelo.Id))
            {
                continue;
            }

            if (Coincide(modelo, texto))
            {
                SugerenciasBusqueda.Add(modelo);
            }
        }
    }

    private static bool Coincide(ModelRecord modelo, string texto) =>
        Contiene(modelo.NombreModelo, texto) ||
        Contiene(modelo.NumeroParteSmt, texto) ||
        Contiene(modelo.NumeroParteBe, texto) ||
        Contiene(modelo.NumeroParteTla, texto);

    private static bool Contiene(string campo, string texto) =>
        !string.IsNullOrEmpty(campo) && campo.Contains(texto, StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            _settings.Load();
            var database = new ModelDatabaseService(_settings.DatabasePath);
            await database.InitializeAsync();

            // Una sola conexión para las dos tablas
            var (modelos, detalles) = await database.GetModelosConDetallesAsync();
            _detallesPorModelo = detalles;

            var terminados = 0;
            var enProceso = 0;
            var pendientes = 0;
            var bloqueados = 0;
            var fueraDePlazo = 0;

            var conteoPorResponsable = ConfigStatusHelper.Responsables.ToDictionary(nombre => nombre, _ => 0);
            var modelosResumen = new List<ModeloResumenItem>();
            var modelosHistorico = new List<ModelRecord>();

            foreach (var modelo in modelos)
            {
                detalles.TryGetValue(modelo.Id, out var detallesDelModelo);
                var resumen = ConfigStatusHelper.ComputeResumenModelo(modelo, detallesDelModelo);

                modelosResumen.Add(new ModeloResumenItem
                {
                    Modelo = modelo,
                    EstatusBd = resumen.EstatusBd,
                    EstatusPlataformaIMES = resumen.EstatusPlataforma,
                    EstatusGeneral = resumen.EstatusGeneral,
                    FechaLimite = resumen.FechaLimite,
                    DiasRetraso = resumen.DiasRetraso
                });

                var yaContadoPorDocumento = false;
                switch (resumen.EstatusGeneral)
                {
                    case ConfigStatusHelper.Terminado: terminados++; modelosHistorico.Add(modelo); break;
                    case ConfigStatusHelper.EnProceso: enProceso++; break;
                    case ConfigStatusHelper.Bloqueado: bloqueados++; yaContadoPorDocumento = true; break;
                    default: pendientes++; break;
                }

                // El documento va aparte del avance: puede estar Terminado y con PDF pendiente
                if (!yaContadoPorDocumento && ConfigStatusHelper.TieneDocumentoPendienteCorreccion(detallesDelModelo))
                {
                    bloqueados++;
                }

                if (resumen.DiasRetraso > 0)
                {
                    fueraDePlazo++;
                }

                // Cuenta todas las áreas, cada una es trabajo de alguien
                foreach (var detalleArea in detallesDelModelo ?? Enumerable.Empty<DetalleConfiguracion>())
                {
                    foreach (var campo in ConfigStatusHelper.CamposDeEstatus(detalleArea))
                    {
                        if (!string.IsNullOrEmpty(campo) && conteoPorResponsable.ContainsKey(campo))
                        {
                            conteoPorResponsable[campo]++;
                        }
                    }
                }
            }

            _modelosResumen = modelosResumen;
            _modelosHistorico = modelosHistorico;

            // Quita los elegidos que ya no están en Histórico
            var idsHistorico = modelosHistorico.Select(m => m.Id).ToHashSet();
            for (var i = ModelosSeleccionados.Count - 1; i >= 0; i--)
            {
                if (!idsHistorico.Contains(ModelosSeleccionados[i].Id))
                {
                    ModelosSeleccionados.RemoveAt(i);
                }
            }
            AplicarFiltroBusqueda();

            var total = modelos.Count;
            var porcentaje = total > 0 ? Math.Round(100.0 * terminados / total) : 0;

            Kpis.Clear();
            Kpis.Add(new KpiItem { Label = "Total de modelos", Value = total.ToString() });
            Kpis.Add(new KpiItem { Label = "Terminados", Value = terminados.ToString(), Tono = "Success" });
            Kpis.Add(new KpiItem { Label = "En Proceso", Value = enProceso.ToString(), Tono = "Warning" });
            Kpis.Add(new KpiItem { Label = "Pendientes", Value = pendientes.ToString(), Tono = "Pendiente" });
            Kpis.Add(new KpiItem { Label = "Pendientes a corrección de documento", Value = bloqueados.ToString(), Tono = "Danger" });
            Kpis.Add(new KpiItem { Label = "Fuera de plazo (sin terminar)", Value = fueraDePlazo.ToString(), Tono = "Danger" });
            Kpis.Add(new KpiItem { Label = "% Completado", Value = $"{porcentaje:0}%" });

            var maxTareas = conteoPorResponsable.Values.DefaultIfEmpty(0).Max();

            Responsables.Clear();
            foreach (var nombre in ConfigStatusHelper.Responsables)
            {
                var count = conteoPorResponsable[nombre];
                Responsables.Add(new ResponsableResumenItem
                {
                    Nombre = nombre,
                    TareasCompletadas = count,
                    Proporcion = maxTareas > 0 ? (double)count / maxTareas : 0
                });
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("EstadisticasViewModel.InitializeAsync", ex);
            ErrorMessage = "No se pudo conectar a la base de datos compartida — revisa tu conexión de red o VPN de la empresa. Más detalles en el registro de errores (Ajustes).";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Excel en otro hilo; general = todo Histórico, si no los elegidos
    public async Task<bool> ExportarExcelAsync(string filePath, bool reporteGeneral)
    {
        IsExportingExcel = true;
        ErrorMessage = null;
        ReportMessage = null;
        UltimoReportePath = null;

        try
        {
            var (modelos, alcance) = ObtenerAlcanceReporte(reporteGeneral);
            if (modelos is null)
            {
                return false;
            }

            var idsElegidos = modelos.Select(m => m.Id).ToHashSet();
            var resumen = _modelosResumen.Where(m => idsElegidos.Contains(m.Modelo.Id)).ToList();
            var detallesFiltrados = modelos
                .Where(m => _detallesPorModelo.ContainsKey(m.Id))
                .ToDictionary(m => m.Id, m => _detallesPorModelo[m.Id]);

            await Task.Run(() => ExcelExportService.Export(filePath, modelos, resumen, detallesFiltrados, alcance));
            ReportMessage = $"Excel exportado: {filePath}";
            UltimoReportePath = filePath;
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("EstadisticasViewModel.ExportarExcelAsync", ex);
            ErrorMessage = $"No se pudo exportar a Excel: {LogService.Redactar(ex.Message)}";
            return false;
        }
        finally
        {
            IsExportingExcel = false;
        }
    }

    // PDF en otro hilo, una página por área
    public async Task<bool> ExportarPdfAsync(string filePath, bool reporteGeneral)
    {
        IsExportingPdf = true;
        ErrorMessage = null;
        ReportMessage = null;
        UltimoReportePath = null;

        try
        {
            var (modelos, alcance) = ObtenerAlcanceReporte(reporteGeneral);
            if (modelos is null)
            {
                return false;
            }

            var detallesFiltrados = modelos
                .Where(m => _detallesPorModelo.ContainsKey(m.Id))
                .ToDictionary(m => m.Id, m => _detallesPorModelo[m.Id]);

            await Task.Run(() => PdfReportService.Export(filePath, modelos, detallesFiltrados, alcance));
            ReportMessage = $"Reporte PDF generado: {filePath}";
            UltimoReportePath = filePath;
            return true;
        }
        catch (Exception ex)
        {
            LogService.LogError("EstadisticasViewModel.ExportarPdfAsync", ex);
            ErrorMessage = $"No se pudo generar el reporte PDF: {LogService.Redactar(ex.Message)}";
            return false;
        }
        finally
        {
            IsExportingPdf = false;
        }
    }

    // Modelos del reporte y texto de alcance
    private (List<ModelRecord>? Modelos, string? Alcance) ObtenerAlcanceReporte(bool reporteGeneral)
    {
        if (reporteGeneral)
        {
            return (_modelosHistorico, null);
        }

        if (ModelosSeleccionados.Count == 0)
        {
            ErrorMessage = "Selecciona al menos un modelo (búscalo y agrégalo) para el reporte especializado.";
            return (null, null);
        }

        var seleccionados = ModelosSeleccionados.ToList();
        var alcance = seleccionados.Count == 1
            ? $"Modelo: {seleccionados[0].NumeroModelo} — {seleccionados[0].NombreModelo}"
            : $"{seleccionados.Count} modelos seleccionados: {string.Join(", ", seleccionados.Select(m => m.NumeroModelo))}";

        return (seleccionados, alcance);
    }
}
