using Alejandria.Controls;
using Alejandria.Models;
using Alejandria.Services;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Views;

// Diálogo de solo lectura para "Ver detalles" en Histórico — muestra la
// configuración que ya quedó guardada por área (el resultado de "Buscar
// en iMES" y de confirmar el serial), sin ningún control editable.
public sealed partial class VerDetallesDialog : ContentDialog
{
    public VerDetallesDialog(ModelRecord modelo, IReadOnlyList<DetalleConfiguracion> detalles)
    {
        InitializeComponent();

        SubtituloText.Text = $"{modelo.NumeroModelo} — {modelo.NombreModelo}";

        var detallesPorArea = detalles.ToDictionary(d => d.Area, d => d);

        foreach (var area in modelo.AreasDisponibles)
        {
            detallesPorArea.TryGetValue(area, out var detalle);
            detalle ??= new DetalleConfiguracion { ModeloId = modelo.Id, Area = area };

            ContenidoStack.Children.Add(ConstruirTarjetaArea(modelo, area, detalle));
        }

        if (modelo.AreasDisponibles.Count == 0)
        {
            ContenidoStack.Children.Add(new TextBlock
            {
                Text = "Este modelo no tiene áreas configuradas.",
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
            });
        }
    }

    private static Border ConstruirTarjetaArea(ModelRecord modelo, string area, DetalleConfiguracion detalle)
    {
        var resumen = ConfigStatusHelper.ComputeResumen(modelo, detalle);

        var encabezado = new Grid();
        encabezado.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        encabezado.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tituloArea = new TextBlock
        {
            Text = area,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        };
        Grid.SetColumn(tituloArea, 0);

        var badge = new StatusBadge { Status = resumen.EstatusGeneral, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(badge, 1);

        encabezado.Children.Add(tituloArea);
        encabezado.Children.Add(badge);

        var contenido = new StackPanel { Spacing = 12 };
        contenido.Children.Add(encabezado);

        contenido.Children.Add(ConstruirSeccion("Configuración Plataforma iMES",
            ("Work Order", detalle.WorkOrder),
            ConfigStatusHelper.EsAreaSmt(area) ? ("Ruta", detalle.Ruta) : ("Flujo", detalle.Flujo),
            ("Línea", detalle.Linea),
            ("PartLineBomMaintenance", detalle.PartLineBomMaintenance)));

        contenido.Children.Add(ConstruirSeccion("Configuración Base de Datos — iMES",
            ("MODELS_PARENTS", detalle.ImesModelsParents),
            ("MODELS_MASTER", detalle.ImesModelsMaster),
            ("MODELS_SERIALNUMBERSPECS", detalle.ImesModelsSerialNumberSpecs),
            ("MODELS_SERIALNUMBERSPECS Packing", detalle.ImesModelsSerialNumberSpecsPacking)));

        return new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)Application.Current.Resources["CardCornerRadius"],
            Padding = new Thickness(20),
            Child = contenido
        };
    }

    private static StackPanel ConstruirSeccion(string titulo, params (string Etiqueta, string Valor)[] campos)
    {
        var seccion = new StackPanel { Spacing = 6 };
        seccion.Children.Add(new TextBlock
        {
            Text = titulo,
            FontWeight = FontWeights.SemiBold,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
        });

        foreach (var (etiqueta, valor) in campos)
        {
            var fila = new Grid { ColumnSpacing = 12 };
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            fila.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var etiquetaBlock = new TextBlock { Text = etiqueta, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(etiquetaBlock, 0);

            var valorBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(valor) ? "(sin capturar)" : valor,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = string.IsNullOrWhiteSpace(valor) ? FontWeights.Normal : FontWeights.SemiBold
            };
            Grid.SetColumn(valorBlock, 1);

            fila.Children.Add(etiquetaBlock);
            fila.Children.Add(valorBlock);
            seccion.Children.Add(fila);
        }

        return seccion;
    }
}
