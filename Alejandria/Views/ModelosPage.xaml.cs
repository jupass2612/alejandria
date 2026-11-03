using Alejandria.Models;
using Alejandria.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

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

    // Clic en la fila abre Configurar
    private void ModelosList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModeloResumenItem item)
        {
            Frame.Navigate(typeof(ModelConfigPage), item.Modelo);
        }
    }

    private void FiltroBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is SelectorBarItem { Tag: string filtro })
        {
            ViewModel.Filtro = filtro;
        }
    }

    private void BuscarAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        BuscarBox.Focus(FocusState.Keyboard);
    }

    private void QuitarFiltros_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.QuitarFiltros();
        FiltroBar.SelectedItem = FiltroBar.Items[0];
    }

    private void IrAAltas_Click(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(AltasModelosPage));
}
