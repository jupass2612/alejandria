using Alejandria.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace Alejandria.Views;

public sealed partial class ImesParentsPage : Page
{
    public HistorialModelosImesViewModel ViewModel { get; } = new();

    public ImesParentsPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Initialize();
        await ViewModel.CargarParentsAsync();
    }

    private void RegresarButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }
}
