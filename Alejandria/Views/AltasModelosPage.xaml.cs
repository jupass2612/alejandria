using System.Linq;
using Alejandria.Models;
using Alejandria.Services;
using Alejandria.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Views;

public sealed partial class AltasModelosPage : Page
{
    public AltasModelosViewModel ViewModel { get; } = new();

    public AltasModelosPage()
    {
        InitializeComponent();
        Loaded += AltasModelosPage_Loaded;
    }

    private async void AltasModelosPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private async void AddModelButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddModelDialog(ViewModel.Records.ToList())
        {
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Result is { } newRecord)
        {
            await ViewModel.AddModelAsync(newRecord);
        }
    }

    private async void EditModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ModelRecord existing })
        {
            return;
        }

        var dialog = new AddModelDialog(existing, ViewModel.Records.ToList())
        {
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && dialog.Result is { } updatedRecord)
        {
            await ViewModel.UpdateModelAsync(updatedRecord);
        }
    }

    private async void DocumentoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ModelRecord record })
        {
            return;
        }

        if (record.TieneDocumentoAdjunto)
        {
            ViewModel.AbrirDocumento(record);
            return;
        }

        await AdjuntarDesdeArchivoAsync(record);
    }

    private async void ReemplazarDocumentoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ModelRecord record })
        {
            return;
        }

        await AdjuntarDesdeArchivoAsync(record);
    }

    private async Task AdjuntarDesdeArchivoAsync(ModelRecord record)
    {
        var archivoOrigen = await FilePickerHelper.PickOpenFileAsync(".pdf");
        if (archivoOrigen is null)
        {
            return;
        }

        var ok = await ViewModel.AdjuntarDocumentoAsync(record, archivoOrigen);
        if (!ok)
        {
            return;
        }

        var areasPendientes = await ViewModel.ObtenerAreasPendientesDeCorreccionAsync(record.Id);
        if (areasPendientes.Count == 0)
        {
            return;
        }

        var confirmDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "¿Ya se corrigió el documento?",
            Content = $"Este modelo tenía el Estado del Documento en \"Pendiente a correcciones\" ({string.Join(", ", areasPendientes)}). ¿El PDF que acabas de adjuntar ya es la versión corregida?",
            PrimaryButtonText = "Sí, marcar como Correcto",
            CloseButtonText = "No, dejarlo pendiente",
            DefaultButton = ContentDialogButton.Primary
        };

        var resultado = await confirmDialog.ShowAsync();
        if (resultado == ContentDialogResult.Primary)
        {
            await ViewModel.ResetearEstadoDocumentoAsync(record.Id, areasPendientes);
        }
    }
}
