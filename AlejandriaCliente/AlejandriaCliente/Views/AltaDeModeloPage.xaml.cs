using System.Diagnostics;
using System.Text.Json;
using AlejandriaCliente.Models;
using AlejandriaCliente.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AlejandriaCliente.Views;

public sealed partial class AltaDeModeloPage : Page
{
    private readonly SolicitudModelo _solicitud = new();
    private Border[] _secciones = Array.Empty<Border>();
    private int _pasoActual;

    // Distinto de null cuando se entró aquí desde "Editar" en el
    // historial — en ese caso, "Generar PDF" reemplaza ese mismo archivo
    // en vez de pedir una ubicación nueva.
    private HistorialEntry? _entradaEditando;

    public AltaDeModeloPage()
    {
        InitializeComponent();

        SerialesListView.ItemsSource = _solicitud.Seriales;
        AreasPcbSapListView.ItemsSource = _solicitud.AreasPcbSap;
        LineasListView.ItemsSource = _solicitud.Lineas;
        OperacionesListView.ItemsSource = _solicitud.Operaciones;
        AprobacionesListView.ItemsSource = _solicitud.Aprobaciones;

        FechaSolicitudPicker.Date = DateTimeOffset.Now;
        PackoutTipoCombo.SelectedIndex = 0;
        ValidarFlujoCheck.IsChecked = true;

        // El llenado es por secciones, una a la vez — para no soltarle a
        // quien lo llena las 8 secciones de un jalón.
        _secciones = new[] { Seccion1, Seccion2, Seccion3, Seccion4, Seccion5, Seccion6, Seccion7, Seccion8 };
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
                var cargado = JsonSerializer.Deserialize<SolicitudModelo>(entrada.DatosJson);
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

    private async void AdjuntarDiagramaButton_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");

        var archivo = await picker.PickSingleFileAsync();
        if (archivo is null)
        {
            return;
        }

        _solicitud.RutaImagenDiagramaFlujo = archivo.Path;
        DiagramaSeleccionadoText.Text = archivo.Name;

        var bitmap = new BitmapImage();
        using (var stream = await archivo.OpenAsync(Windows.Storage.FileAccessMode.Read))
        {
            await bitmap.SetSourceAsync(stream);
        }

        DiagramaPreviewImage.Source = bitmap;
        DiagramaPreviewImage.Visibility = Visibility.Visible;
    }

    private void AgregarSerialButton_Click(object sender, RoutedEventArgs e) => _solicitud.Seriales.Add(new SerialEntry());

    private void QuitarSerialButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SerialEntry entry })
        {
            _solicitud.Seriales.Remove(entry);
        }
    }

    private void AgregarLineaButton_Click(object sender, RoutedEventArgs e) => _solicitud.Lineas.Add(new LineaInfo());

    private void QuitarLineaButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LineaInfo linea })
        {
            _solicitud.Lineas.Remove(linea);
        }
    }

    private void AgregarOperacionButton_Click(object sender, RoutedEventArgs e) => _solicitud.Operaciones.Add(new OperacionInfo());

    private void QuitarOperacionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OperacionInfo operacion })
        {
            _solicitud.Operaciones.Remove(operacion);
        }
    }

    private async Task GenerarPdfAsync()
    {
        ResultadoInfoBar.IsOpen = false;

        if (string.IsNullOrWhiteSpace(NombreModeloBox.Text))
        {
            MostrarResultado(InfoBarSeverity.Warning, "Captura al menos el nombre del modelo antes de generar el PDF.");
            return;
        }

        VolcarCamposASolicitud();

        string rutaDestino;

        if (_entradaEditando is not null)
        {
            // Modo edición: se reemplaza el mismo archivo, sin preguntar dónde guardar.
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
            picker.SuggestedFileName = $"New_Part_Number_{_solicitud.NombreModelo.Replace(' ', '_')}";
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
            SolicitudPdfService.Export(rutaDestino, _solicitud);

            var entrada = _entradaEditando ?? new HistorialEntry { Tipo = TipoSolicitud.AltaDeModelo, RutaPdf = rutaDestino };
            entrada.NombreModelo = _solicitud.NombreModelo;
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

    // Las listas dinámicas (seriales, líneas, operaciones, áreas PCB/SAP,
    // aprobaciones) ya se editan con enlace directo en el XAML — aquí solo
    // hace falta volcar los campos sueltos que no vienen de una lista.
    private void VolcarCamposASolicitud()
    {
        _solicitud.FechaSolicitud = FechaSolicitudPicker.Date;
        _solicitud.FechaEsperada = FechaEsperadaPicker.Date;
        _solicitud.NombreSolicitante = NombreSolicitanteBox.Text;
        _solicitud.CorreoSolicitante = CorreoSolicitanteBox.Text;

        _solicitud.NombreModelo = NombreModeloBox.Text;
        _solicitud.SapPartNumberFG = SapFgBox.Text;
        _solicitud.SapPartNumberPCBA = SapPcbaBox.Text;
        _solicitud.SapPartNumberTC = SapTcBox.Text;
        _solicitud.BcsPartNumber = BcsPartNumberBox.Text;
        _solicitud.ClientPartNumberFG = ClientFgBox.Text;
        _solicitud.ClientPartNumberPCBA = ClientPcbaBox.Text;

        _solicitud.PcbsPerPanel = PcbsPerPanelBox.Text;

        _solicitud.PackoutTipo = PackoutTipoCombo.SelectedItem as string ?? "FG";
        _solicitud.ValidarFlujoAntesDePack = ValidarFlujoCheck.IsChecked ?? false;
        _solicitud.UltimaOperacionAntesDePackout = UltimaOperacionBox.Text;
        _solicitud.NuevaEstacionPackout = NuevaEstacionCheck.IsChecked ?? false;
        _solicitud.FechaInstalacionEstacion = NuevaEstacionCheck.IsChecked == true ? FechaInstalacionPicker.Date : null;
        _solicitud.BoxLabelQr = BoxLabelBox.Text;
        _solicitud.ProductLabelQr = ProductLabelBox.Text;
    }

    // El reverso de lo de arriba — llena la pantalla con los datos de una
    // solicitud ya guardada (usado por "Editar" desde el historial).
    private void CargarCamposDesdeSolicitud(SolicitudModelo cargado)
    {
        FechaSolicitudPicker.Date = cargado.FechaSolicitud;
        FechaEsperadaPicker.Date = cargado.FechaEsperada ?? DateTimeOffset.Now;
        NombreSolicitanteBox.Text = cargado.NombreSolicitante;
        CorreoSolicitanteBox.Text = cargado.CorreoSolicitante;

        NombreModeloBox.Text = cargado.NombreModelo;
        SapFgBox.Text = cargado.SapPartNumberFG;
        SapPcbaBox.Text = cargado.SapPartNumberPCBA;
        SapTcBox.Text = cargado.SapPartNumberTC;
        BcsPartNumberBox.Text = cargado.BcsPartNumber;
        ClientFgBox.Text = cargado.ClientPartNumberFG;
        ClientPcbaBox.Text = cargado.ClientPartNumberPCBA;

        _solicitud.Seriales.Clear();
        foreach (var serial in cargado.Seriales)
        {
            _solicitud.Seriales.Add(serial);
        }

        PcbsPerPanelBox.Text = cargado.PcbsPerPanel;
        _solicitud.AreasPcbSap.Clear();
        foreach (var area in cargado.AreasPcbSap)
        {
            _solicitud.AreasPcbSap.Add(area);
        }

        _solicitud.Lineas.Clear();
        foreach (var linea in cargado.Lineas)
        {
            _solicitud.Lineas.Add(linea);
        }

        if (!string.IsNullOrWhiteSpace(cargado.RutaImagenDiagramaFlujo))
        {
            _solicitud.RutaImagenDiagramaFlujo = cargado.RutaImagenDiagramaFlujo;
            DiagramaSeleccionadoText.Text = File.Exists(cargado.RutaImagenDiagramaFlujo)
                ? Path.GetFileName(cargado.RutaImagenDiagramaFlujo)
                : "(la imagen original ya no está en esa ubicación — adjunta una de nuevo si hace falta)";
        }

        _solicitud.Operaciones.Clear();
        foreach (var operacion in cargado.Operaciones)
        {
            _solicitud.Operaciones.Add(operacion);
        }

        var indicePackout = PackoutTipoCombo.Items.OfType<string>().ToList().IndexOf(cargado.PackoutTipo);
        PackoutTipoCombo.SelectedIndex = indicePackout >= 0 ? indicePackout : 0;
        ValidarFlujoCheck.IsChecked = cargado.ValidarFlujoAntesDePack;
        UltimaOperacionBox.Text = cargado.UltimaOperacionAntesDePackout;
        NuevaEstacionCheck.IsChecked = cargado.NuevaEstacionPackout;
        if (cargado.FechaInstalacionEstacion.HasValue)
        {
            FechaInstalacionPicker.Date = cargado.FechaInstalacionEstacion.Value;
        }

        BoxLabelBox.Text = cargado.BoxLabelQr;
        ProductLabelBox.Text = cargado.ProductLabelQr;

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
