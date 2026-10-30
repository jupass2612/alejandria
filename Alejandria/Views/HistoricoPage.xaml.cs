using Alejandria.Models;
using Alejandria.Services;
using Alejandria.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Views;

public sealed partial class HistoricoPage : Page
{
    public HistoricoViewModel ViewModel { get; } = new();

    public HistoricoPage()
    {
        InitializeComponent();
        Loaded += HistoricoPage_Loaded;
    }

    private async void HistoricoPage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.FiltroTexto = SearchBox.Text;
    }

    private async void VerDetallesButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ModelRecord modelo })
        {
            return;
        }

        var detalles = await ViewModel.ObtenerDetallesAsync(modelo);

        var dialog = new VerDetallesDialog(modelo, detalles)
        {
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void ActualizarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ModelRecord original })
        {
            return;
        }

        var dialog = new UpdateModelDialog(original)
        {
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || dialog.Result is not { } nuevo)
        {
            return;
        }

        var creado = await ViewModel.CrearActualizacionAsync(original, nuevo, dialog.AreasCambiadas);
        if (creado is null)
        {
            return;
        }

        await PedirJustificacionAsync(creado);

        // El modelo nuevo empieza sin configurar (al menos en el área que
        // cambió) — se manda directo a llenarla, en vez de dejarlo perdido
        // en la pantalla de Modelos.
        Frame.Navigate(typeof(ModelConfigPage), creado);
    }

    private async void AdjuntarPdfCorregidoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ModelRecord modelo })
        {
            return;
        }

        var archivoOrigen = await FilePickerHelper.PickOpenFileAsync(".pdf");
        if (archivoOrigen is null)
        {
            return;
        }

        var ok = await ViewModel.AdjuntarPdfCorregidoAsync(modelo, archivoOrigen);
        if (!ok)
        {
            var errorDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "No se pudo adjuntar",
                Content = "No se pudo guardar el PDF corregido. Revisa tu conexión de red e inténtalo de nuevo.",
                CloseButtonText = "Entendido"
            };
            await errorDialog.ShowAsync();
            return;
        }

        var confirmacion = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "PDF actualizado",
            Content = $"Se guardó el PDF corregido para \"{modelo.NumeroModelo} — {modelo.NombreModelo}\". El Estado del Documento ya se regresó a Correcto.",
            CloseButtonText = "Entendido"
        };
        await confirmacion.ShowAsync();

        await ViewModel.InitializeAsync();
    }

    // Pide (sin obligar) el PDF que justifica el cambio de número de parte.
    private async Task PedirJustificacionAsync(ModelRecord nuevo)
    {
        var pedirDialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Justificación del cambio",
            Content = $"Se creó \"{nuevo.NumeroModelo} — {nuevo.NombreModelo}\". Antes de configurarlo, adjunta el PDF que justifica este cambio de número de parte (por ejemplo, el ECN o la notificación del cambio) — se guarda igual que en Altas de Modelos.",
            PrimaryButtonText = "Adjuntar PDF",
            CloseButtonText = "Omitir por ahora",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await pedirDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var archivoOrigen = await FilePickerHelper.PickOpenFileAsync(".pdf");
        if (archivoOrigen is null)
        {
            return;
        }

        var ok = await ViewModel.AdjuntarJustificacionAsync(nuevo, archivoOrigen);
        if (!ok)
        {
            var errorDialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "No se pudo adjuntar",
                Content = "El modelo se creó de todas formas. Puedes intentar adjuntar el PDF de justificación después, desde \"Altas de Modelos\".",
                CloseButtonText = "Entendido"
            };
            await errorDialog.ShowAsync();
        }
    }
}
