using Alejandria.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Alejandria.Views;

public sealed partial class ConfigurarEnImesPage : Page
{
    public ConfigurarEnImesPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ConfigurarEnImesNavParams parametros)
        {
            Wizard.InicializarParaModelo(parametros.Modelo, parametros.Area);
            ModeloAreaText.Text = $"{parametros.Modelo.NumeroModelo} — {parametros.Modelo.NombreModelo}  ·  Área: {parametros.Area}";
            ModeloAreaText.Visibility = Visibility.Visible;
        }
        else
        {
            Wizard.Inicializar();
        }

        EntornoActivoText.Text = Wizard.ViewModel.EntornoActivoTexto;
    }

    private void RegresarButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }
}
