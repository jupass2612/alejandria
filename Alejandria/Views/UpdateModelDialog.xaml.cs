using System.Collections.Generic;
using System.Linq;
using Alejandria.Models;
using Alejandria.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Views;

// Modelo nuevo a partir de uno en Histórico cuando cambió el número de parte.
// Solo cambian las áreas marcadas
public sealed partial class UpdateModelDialog : ContentDialog
{
    private readonly ModelRecord _original;

    // El modelo nuevo, después de Crear actualización
    public ModelRecord? Result { get; private set; }

    // Áreas que cambiaron, las demás heredan la config
    public HashSet<string> AreasCambiadas { get; } = new();

    public UpdateModelDialog(ModelRecord original)
    {
        InitializeComponent();
        _original = original;

        // Misma lista que Configurar
        QuienRecibeBox.ItemsSource = ConfigStatusHelper.Responsables;

        ModeloOrigenText.Text =
            $"Creando una actualización de \"{original.NumeroModelo} — {original.NombreModelo}\". " +
            "Elige abajo qué área(s) cambiaron de número de parte.";

        ConfigurarFila(SmtRow, SmtNuevoBox, original.NumeroParteSmt);
        ConfigurarFila(BeRow, BeNuevoBox, original.NumeroParteBe);
        ConfigurarFila(TlaRow, TlaNuevoBox, original.NumeroParteTla);

        FechaSolicitudPicker.Date = DateTimeOffset.Now;
        SugerirNombre();
    }

    // Oculta la fila si el original no tiene esa área
    private static void ConfigurarFila(StackPanel fila, TextBox nuevoBox, string numeroActual)
    {
        if (string.IsNullOrWhiteSpace(numeroActual))
        {
            fila.Visibility = Visibility.Collapsed;
            return;
        }

        nuevoBox.PlaceholderText = $"Actual: {numeroActual}";
    }

    private void AreaCambioCheck_Changed(object sender, RoutedEventArgs e)
    {
        SmtNuevoBox.IsEnabled = SmtCambioCheck.IsChecked == true;
        BeNuevoBox.IsEnabled = BeCambioCheck.IsChecked == true;
        TlaNuevoBox.IsEnabled = TlaCambioCheck.IsChecked == true;
        SugerirNombre();
    }

    // Nombre sugerido según los checkboxes
    private void SugerirNombre()
    {
        var areas = new List<string>();
        if (SmtCambioCheck.IsChecked == true) areas.Add("SMT");
        if (BeCambioCheck.IsChecked == true) areas.Add("BE");
        if (TlaCambioCheck.IsChecked == true) areas.Add("TLA");

        NombreModeloBox.Text = areas.Count == 0
            ? _original.NombreModelo
            : $"{_original.NombreModelo} — Actualización de {string.Join("/", areas)}";
    }

    // Número nuevo si cambió, si no el original
    private static (string Numero, bool Cambio) ResolverArea(CheckBox check, TextBox nuevoBox, string numeroOriginal)
    {
        if (check.IsChecked == true)
        {
            return (nuevoBox.Text?.Trim() ?? string.Empty, true);
        }

        return (numeroOriginal, false);
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var nombre = NombreModeloBox.Text?.Trim() ?? string.Empty;
        var quienRecibe = QuienRecibeBox.SelectedItem as string ?? string.Empty;

        var (smt, smtCambio) = ResolverArea(SmtCambioCheck, SmtNuevoBox, _original.NumeroParteSmt);
        var (be, beCambio) = ResolverArea(BeCambioCheck, BeNuevoBox, _original.NumeroParteBe);
        var (tla, tlaCambio) = ResolverArea(TlaCambioCheck, TlaNuevoBox, _original.NumeroParteTla);

        var algunaAreaCambio = smtCambio || beCambio || tlaCambio;
        var numerosNuevosCompletos =
            (!smtCambio || !string.IsNullOrWhiteSpace(smt)) &&
            (!beCambio || !string.IsNullOrWhiteSpace(be)) &&
            (!tlaCambio || !string.IsNullOrWhiteSpace(tla));

        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(quienRecibe) || !algunaAreaCambio || !numerosNuevosCompletos)
        {
            ValidationInfoBar.IsOpen = true;
            args.Cancel = true;
            return;
        }

        AreasCambiadas.Clear();
        if (smtCambio) AreasCambiadas.Add("SMT");
        if (beCambio) AreasCambiadas.Add("BE");
        if (tlaCambio) AreasCambiadas.Add("TLA");

        // Prioridad SMT > BE > TLA
        var numeroModelo = !string.IsNullOrWhiteSpace(smt) ? smt
            : !string.IsNullOrWhiteSpace(be) ? be
            : tla;

        Result = new ModelRecord
        {
            NumeroModelo = numeroModelo,
            NombreModelo = nombre,
            NumeroParteSmt = smt,
            NumeroParteBe = be,
            NumeroParteTla = tla,
            QuienRecibeDocumento = quienRecibe,
            FechaSolicitud = FechaSolicitudPicker.Date.Date,
            QuienMandoDocumento = QuienMandaBox.Text?.Trim() ?? string.Empty
        };
    }
}
