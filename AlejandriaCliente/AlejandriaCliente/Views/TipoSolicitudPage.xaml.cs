using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AlejandriaCliente.Views;

public sealed partial class TipoSolicitudPage : Page
{
    public TipoSolicitudPage()
    {
        InitializeComponent();
    }

    private void AltaDeModeloButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(AltaDeModeloPage));

    private void CambioDeFlujoButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CambioDeFlujoPage));

    private void CambioDeModeloButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(CambioDeModeloPage));

    private void VerHistorialButton_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(HistorialPage));
}
