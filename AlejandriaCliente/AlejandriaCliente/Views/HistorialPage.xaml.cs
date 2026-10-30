using System.Diagnostics;
using AlejandriaCliente.Models;
using AlejandriaCliente.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace AlejandriaCliente.Views;

public sealed partial class HistorialPage : Page
{
    public HistorialPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        CargarHistorial();
    }

    private void CargarHistorial()
    {
        var entradas = HistorialService.Listar();
        HistorialListView.ItemsSource = entradas;
        SinHistorialText.Visibility = entradas.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RegresarButton_Click(object sender, RoutedEventArgs e) => Frame.GoBack();

    private void VerPdfButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HistorialEntry entrada })
        {
            return;
        }

        var ruta = File.Exists(entrada.RutaPdf) ? entrada.RutaPdf : entrada.RutaPdfRespaldo;
        if (!File.Exists(ruta))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
    }

    private void EditarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: HistorialEntry entrada })
        {
            return;
        }

        var tipoPagina = entrada.Tipo switch
        {
            TipoSolicitud.AltaDeModelo => typeof(AltaDeModeloPage),
            TipoSolicitud.CambioDeFlujo => typeof(CambioDeFlujoPage),
            TipoSolicitud.CambioDeModelo => typeof(CambioDeModeloPage),
            _ => typeof(AltaDeModeloPage)
        };

        Frame.Navigate(tipoPagina, entrada);
    }
}
