using AlejandriaCliente.Views;
using Microsoft.UI.Xaml;

namespace AlejandriaCliente;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = "Alejandría Cliente";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 900));

        ContentFrame.Navigate(typeof(TipoSolicitudPage));
    }
}
