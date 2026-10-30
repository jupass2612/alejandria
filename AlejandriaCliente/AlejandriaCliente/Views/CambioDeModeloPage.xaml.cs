using System.Diagnostics;
using System.Text.Json;
using AlejandriaCliente.Models;
using AlejandriaCliente.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AlejandriaCliente.Views;

public sealed partial class CambioDeModeloPage : Page
{
    private readonly SolicitudCambioModelo _solicitud = new();
    private Border[] _secciones = Array.Empty<Border>();
    private int _pasoActual;
    private HistorialEntry? _entradaEditando;

    public CambioDeModeloPage()
    {
        InitializeComponent();

        AreasPcbSapListView.ItemsSource = _solicitud.AreasPcbSap;
        AprobacionesListView.ItemsSource = _solicitud.Aprobaciones;

        FechaSolicitudPicker.Date = DateTimeOffset.Now;

        _secciones = new[] { Seccion1, Seccion2, Seccion3, Seccion4, Seccion5 };
        MostrarPaso(0);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is HistorialEntry entrada)
        {
            _entradaEditando = entrada;
            try
            {
                var cargado = JsonSerializer.Deserialize<SolicitudCambioModelo>(entrada.DatosJson);
                if (cargado is not null)
                {
                    CargarCamposDesdeSolicitud(cargado);
                }
            }
            catch (Exception ex)
            {
                MostrarResultado(InfoBarSeverity.Warning, $"No se pudieron cargar los datos guardados: {ex.Message}");
            }
        }
    }

    private void MostrarPaso(int indice)
    {
        _pasoActual = indice;

        for (var i = 0; i < _secciones.Length; i++)
        {
            _secciones[i].Visibility = i == indice ? Visibility.Visible : Visibility.Collapsed;
        }

        PasoActualText.Text = $"Paso {indice + 1} de {_secciones.Length}";
        AtrasButton.IsEnabled = indice > 0;
        SiguienteButton.Content = indice == _secciones.Length - 1 ? "Generar PDF" : "Siguiente >";
    }

    private void AtrasButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pasoActual > 0)
        {
            MostrarPaso(_pasoActual - 1);
        }
    }

    private async void SiguienteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pasoActual < _secciones.Length - 1)
        {
            MostrarPaso(_pasoActual + 1);
            return;
        }

        await GenerarPdfAsync();
    }

    private void RegresarButton_Click(object sender, RoutedEventArgs e) => Frame.GoBack();

    private async Task GenerarPdfAsync()
    {
        ResultadoInfoBar.IsOpen = false;

        if (string.IsNullOrWhiteSpace(ModelNameActualBox.Text) && string.IsNullOrWhiteSpace(ModelNameNuevoBox.Text))
        {
            MostrarResultado(InfoBarSeverity.Warning, "Captura al menos el nombre del modelo (actual o nuevo) antes de generar el PDF.");
            return;
        }

        VolcarCamposASolicitud();

        string rutaDestino;

        if (_entradaEditando is not null)
        {
            rutaDestino = _entradaEditando.RutaPdf;
        }
        else
        {
            if (App.MainWindow is null)
            {
                return;
            }

            var picker = new FileSavePicker();
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
            var nombreBase = string.IsNullOrWhiteSpace(_solicitud.ModelName.Nuevo) ? _solicitud.ModelName.Actual : _solicitud.ModelName.Nuevo;
            picker.SuggestedFileName = $"Model_Change_{nombreBase.Replace(' ', '_')}";
            picker.FileTypeChoices.Add("Documento PDF", new List<string> { ".pdf" });

            var archivo = await picker.PickSaveFileAsync();
            if (archivo is null)
            {
                return;
            }

            rutaDestino = archivo.Path;
        }

        try
        {
            SolicitudPdfService.ExportCambioDeModelo(rutaDestino, _solicitud);

            var nombreParaHistorial = string.IsNullOrWhiteSpace(_solicitud.ModelName.Nuevo) ? _solicitud.ModelName.Actual : _solicitud.ModelName.Nuevo;
            var entrada = _entradaEditando ?? new HistorialEntry { Tipo = TipoSolicitud.CambioDeModelo, RutaPdf = rutaDestino };
            entrada.NombreModelo = nombreParaHistorial;
            entrada.Fecha = DateTimeOffset.Now;
            entrada.DatosJson = JsonSerializer.Serialize(_solicitud);
            HistorialService.GuardarOActualizar(entrada, rutaDestino);
            _entradaEditando = entrada;

            MostrarResultado(InfoBarSeverity.Success, "PDF generado — ya lo puedes adjuntar en Altas de Modelos.");
            Process.Start(new ProcessStartInfo(rutaDestino) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MostrarResultado(InfoBarSeverity.Warning, $"No se pudo generar el PDF: {ex.Message}");
        }
    }

    private void VolcarCamposASolicitud()
    {
        _solicitud.FechaSolicitud = FechaSolicitudPicker.Date;
        _solicitud.FechaEsperada = FechaEsperadaPicker.Date;
        _solicitud.NombreSolicitante = NombreSolicitanteBox.Text;
        _solicitud.CorreoSolicitante = CorreoSolicitanteBox.Text;

        _solicitud.ModelName.Actual = ModelNameActualBox.Text;
        _solicitud.ModelName.Nuevo = ModelNameNuevoBox.Text;
        _solicitud.ModelSapPartNumber.Actual = SapPartNumberActualBox.Text;
        _solicitud.ModelSapPartNumber.Nuevo = SapPartNumberNuevoBox.Text;
        _solicitud.BcsPartNumber.Actual = BcsPartNumberActualBox.Text;
        _solicitud.BcsPartNumber.Nuevo = BcsPartNumberNuevoBox.Text;
        _solicitud.ClientPartNumber.Actual = ClientPartNumberActualBox.Text;
        _solicitud.ClientPartNumber.Nuevo = ClientPartNumberNuevoBox.Text;
        _solicitud.LaserSerialNumber.Actual = LaserSerialActualBox.Text;
        _solicitud.LaserSerialNumber.Nuevo = LaserSerialNuevoBox.Text;

        _solicitud.PcbsPerPanel.Actual = PcbsPerPanelActualBox.Text;
        _solicitud.PcbsPerPanel.Nuevo = PcbsPerPanelNuevoBox.Text;

        _solicitud.PackoutTipo.Actual = PackoutTipoActualBox.Text;
        _solicitud.PackoutTipo.Nuevo = PackoutTipoNuevoBox.Text;
        _solicitud.ValidarFlujoAntesDePack.Actual = ValidarFlujoActualBox.Text;
        _solicitud.ValidarFlujoAntesDePack.Nuevo = ValidarFlujoNuevoBox.Text;
        _solicitud.UltimaOperacionAntesDePackout.Actual = UltimaOperacionActualBox.Text;
        _solicitud.UltimaOperacionAntesDePackout.Nuevo = UltimaOperacionNuevoBox.Text;
        _solicitud.NuevaEstacionPackout.Actual = NuevaEstacionActualBox.Text;
        _solicitud.NuevaEstacionPackout.Nuevo = NuevaEstacionNuevoBox.Text;
        _solicitud.BoxLabelQr.Actual = BoxLabelActualBox.Text;
        _solicitud.BoxLabelQr.Nuevo = BoxLabelNuevoBox.Text;
        _solicitud.ProductLabelQr.Actual = ProductLabelActualBox.Text;
        _solicitud.ProductLabelQr.Nuevo = ProductLabelNuevoBox.Text;
    }

    // El reverso de lo de arriba — llena la pantalla con los datos de una
    // solicitud ya guardada (usado por "Editar" desde el historial).
    private void CargarCamposDesdeSolicitud(SolicitudCambioModelo cargado)
    {
        FechaSolicitudPicker.Date = cargado.FechaSolicitud;
        FechaEsperadaPicker.Date = cargado.FechaEsperada ?? DateTimeOffset.Now;
        NombreSolicitanteBox.Text = cargado.NombreSolicitante;
        CorreoSolicitanteBox.Text = cargado.CorreoSolicitante;

        ModelNameActualBox.Text = cargado.ModelName.Actual;
        ModelNameNuevoBox.Text = cargado.ModelName.Nuevo;
        SapPartNumberActualBox.Text = cargado.ModelSapPartNumber.Actual;
        SapPartNumberNuevoBox.Text = cargado.ModelSapPartNumber.Nuevo;
        BcsPartNumberActualBox.Text = cargado.BcsPartNumber.Actual;
        BcsPartNumberNuevoBox.Text = cargado.BcsPartNumber.Nuevo;
        ClientPartNumberActualBox.Text = cargado.ClientPartNumber.Actual;
        ClientPartNumberNuevoBox.Text = cargado.ClientPartNumber.Nuevo;
        LaserSerialActualBox.Text = cargado.LaserSerialNumber.Actual;
        LaserSerialNuevoBox.Text = cargado.LaserSerialNumber.Nuevo;

        PcbsPerPanelActualBox.Text = cargado.PcbsPerPanel.Actual;
        PcbsPerPanelNuevoBox.Text = cargado.PcbsPerPanel.Nuevo;

        _solicitud.AreasPcbSap.Clear();
        foreach (var area in cargado.AreasPcbSap)
        {
            _solicitud.AreasPcbSap.Add(area);
        }

        PackoutTipoActualBox.Text = cargado.PackoutTipo.Actual;
        PackoutTipoNuevoBox.Text = cargado.PackoutTipo.Nuevo;
        ValidarFlujoActualBox.Text = cargado.ValidarFlujoAntesDePack.Actual;
        ValidarFlujoNuevoBox.Text = cargado.ValidarFlujoAntesDePack.Nuevo;
        UltimaOperacionActualBox.Text = cargado.UltimaOperacionAntesDePackout.Actual;
        UltimaOperacionNuevoBox.Text = cargado.UltimaOperacionAntesDePackout.Nuevo;
        NuevaEstacionActualBox.Text = cargado.NuevaEstacionPackout.Actual;
        NuevaEstacionNuevoBox.Text = cargado.NuevaEstacionPackout.Nuevo;
        BoxLabelActualBox.Text = cargado.BoxLabelQr.Actual;
        BoxLabelNuevoBox.Text = cargado.BoxLabelQr.Nuevo;
        ProductLabelActualBox.Text = cargado.ProductLabelQr.Actual;
        ProductLabelNuevoBox.Text = cargado.ProductLabelQr.Nuevo;

        _solicitud.Aprobaciones.Clear();
        foreach (var aprobacion in cargado.Aprobaciones)
        {
            _solicitud.Aprobaciones.Add(aprobacion);
        }
    }

    private void MostrarResultado(InfoBarSeverity severidad, string mensaje)
    {
        ResultadoInfoBar.Severity = severidad;
        ResultadoInfoBar.Message = mensaje;
        ResultadoInfoBar.IsOpen = true;
    }
}
