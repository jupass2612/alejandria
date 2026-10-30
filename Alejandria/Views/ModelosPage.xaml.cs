using Alejandria.Models;
using Alejandria.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Views;

public sealed partial class ModelosPage : Page
{
    public ModelosViewModel ViewModel { get; } = new();

    public ModelosPage()
    {
        InitializeComponent();
        Loaded += ModelosPage_Loaded;
    }

    private async void ModelosPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void ConfigurarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ModelRecord modelo })
        {
            Frame.Navigate(typeof(ModelConfigPage), modelo);
        }
    }
}
