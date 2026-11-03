using System;
using System.IO;
using Alejandria.Models;
using Alejandria.Services;
using Alejandria.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Alejandria.Views;

public sealed partial class EstadisticasPage : Page
{
    public EstadisticasViewModel ViewModel { get; } = new();

    public EstadisticasPage()
    {
        InitializeComponent();
        Loaded += EstadisticasPage_Loaded;
    }

    private async void EstadisticasPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadButtonLogos();
        await ViewModel.InitializeAsync();
    }

    private void LoadButtonLogos()
    {
        try
        {
            var excelIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ExcelIcon.png");
            if (File.Exists(excelIconPath))
            {
                ExcelButtonLogo.Source = new BitmapImage(new Uri(excelIconPath));
            }

            var pdfIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "PdfIcon.png");
            if (File.Exists(pdfIconPath))
            {
                PdfButtonLogo.Source = new BitmapImage(new Uri(pdfIconPath));
            }
        }
        catch (Exception)
        {
            // El ícono es decorativo
        }
    }

    private void AlcanceRadioButtons_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        EspecializadoPanel.Visibility = AlcanceRadioButtons.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BusquedaModeloBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.BusquedaModelo = BusquedaModeloBox.Text;
    }

    private void AgregarSugerenciaButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ModelRecord modelo })
        {
            return;
        }

        ViewModel.AgregarModeloSeleccionado(modelo);

        // Limpia para el siguiente
        BusquedaModeloBox.Text = string.Empty;
    }

    private void QuitarSeleccionadoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ModelRecord modelo })
        {
            ViewModel.QuitarModeloSeleccionado(modelo);
        }
    }

    // Al menos un modelo elegido
    private bool ValidarAlcance(bool reporteGeneral)
    {
        if (reporteGeneral || ViewModel.ModelosSeleccionados.Count > 0)
        {
            return true;
        }

        ViewModel.UltimoReportePath = null;
        ReportInfoBar.Severity = InfoBarSeverity.Error;
        ReportInfoBar.Title = "Faltan modelos";
        ReportInfoBar.Message = "Busca y agrega al menos un modelo para el reporte especializado.";
        ReportInfoBar.IsOpen = true;
        return false;
    }

    private string SugerirNombreArchivo(string prefijo, bool reporteGeneral)
    {
        if (reporteGeneral)
        {
            return prefijo;
        }

        var seleccionados = ViewModel.ModelosSeleccionados;
        return seleccionados.Count == 1
            ? $"{prefijo}_{seleccionados[0].NumeroModelo}"
            : $"{prefijo}_{seleccionados.Count}modelos";
    }

    private async void ExportarExcelButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsExportingExcel)
        {
            return;
        }

        var reporteGeneral = AlcanceRadioButtons.SelectedIndex == 0;
        if (!ValidarAlcance(reporteGeneral))
        {
            return;
        }

        var sugerido = SugerirNombreArchivo("AlejandriaReporte", reporteGeneral);
        var path = await FilePickerHelper.PickSaveFileAsync(sugerido, "Libro de Excel", ".xlsx");
        if (path is null)
        {
            return;
        }

        ReportInfoBar.Severity = InfoBarSeverity.Success;
        ReportInfoBar.Title = "Listo";
        ReportInfoBar.IsOpen = false;
        var ok = await ViewModel.ExportarExcelAsync(path, reporteGeneral);
        ReportInfoBar.IsOpen = ok;
    }

    private async void GenerarPdfButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsExportingPdf)
        {
            return;
        }

        var reporteGeneral = AlcanceRadioButtons.SelectedIndex == 0;
        if (!ValidarAlcance(reporteGeneral))
        {
            return;
        }

        var sugerido = SugerirNombreArchivo("AlejandriaReporteConfiguracion", reporteGeneral);
        var path = await FilePickerHelper.PickSaveFileAsync(sugerido, "Documento PDF", ".pdf");
        if (path is null)
        {
            return;
        }

        ReportInfoBar.Severity = InfoBarSeverity.Success;
        ReportInfoBar.Title = "Listo";
        ReportInfoBar.IsOpen = false;
        var ok = await ViewModel.ExportarPdfAsync(path, reporteGeneral);
        ReportInfoBar.IsOpen = ok;
    }

    // Abre el reporte
    private void AbrirReporteButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.UltimoReportePath))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ViewModel.UltimoReportePath) { UseShellExecute = true });
        }
        catch (Exception)
        {
            ReportInfoBar.Severity = InfoBarSeverity.Error;
            ReportInfoBar.Title = "No se pudo abrir";
            ReportInfoBar.Message = "El archivo no se pudo abrir automáticamente — puedes buscarlo manualmente en la ruta que elegiste al guardarlo.";
            ReportInfoBar.IsOpen = true;
        }
    }
}
