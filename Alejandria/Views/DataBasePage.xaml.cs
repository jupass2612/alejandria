using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Views;

public sealed partial class DataBasePage : Page
{
    public DataBasePage()
    {
        InitializeComponent();
    }

    private void ConfigurarEnImesButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ConfigurarEnImesPage));
    }

    private void VerParentsButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ImesParentsPage));
    }

    private void VerMasterButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ImesMasterPage));
    }

    private void VerSerialSpecsButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ImesSerialSpecsPage));
    }
}
