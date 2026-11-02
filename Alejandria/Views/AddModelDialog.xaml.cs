using System.Linq;
using Alejandria.Models;
using Alejandria.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Views;

// Formulario para dar de alta un modelo o editar uno existente.
public sealed partial class AddModelDialog : ContentDialog
{
    private readonly int _editingId;
    private readonly IReadOnlyList<ModelRecord> _existingRecords;

    // El registro capturado, disponible después de "Guardar".
    public ModelRecord? Result { get; private set; }

    public AddModelDialog() : this(null, Array.Empty<ModelRecord>())
    {
    }

    public AddModelDialog(IReadOnlyList<ModelRecord> existingRecords) : this(null, existingRecords)
    {
    }

    public AddModelDialog(ModelRecord? existing, IReadOnlyList<ModelRecord> existingRecords)
    {
        InitializeComponent();
        _existingRecords = existingRecords;

        // Misma lista que los selectores de Configurar
        QuienRecibeBox.ItemsSource = ConfigStatusHelper.Responsables;

        if (existing is not null)
        {
            _editingId = existing.Id;
            Title = "Editar modelo";
            PrimaryButtonText = "Guardar cambios";

            NombreModeloBox.Text = existing.NombreModelo;
            NumeroParteSmtBox.Text = existing.NumeroParteSmt;
            NumeroParteBeBox.Text = existing.NumeroParteBe;
            NumeroParteTlaBox.Text = existing.NumeroParteTla;
            QuienRecibeBox.SelectedItem = ConfigStatusHelper.Responsables.FirstOrDefault(r => r == ConfigStatusHelper.NormalizarResponsable(existing.QuienRecibeDocumento));
            FechaSolicitudPicker.Date = new DateTimeOffset(existing.FechaSolicitud);
            QuienMandaBox.Text = existing.QuienMandoDocumento;

            // Si ya coinciden, se marca el checkbox solo — así queda claro
            // que es la misma pieza y sigue en sincronía si se edita SMT.
            if (!string.IsNullOrWhiteSpace(existing.NumeroParteSmt) && existing.NumeroParteSmt == existing.NumeroParteBe)
            {
                BeMismoQueSmtCheck.IsChecked = true;
            }
            if (!string.IsNullOrWhiteSpace(existing.NumeroParteSmt) && existing.NumeroParteSmt == existing.NumeroParteTla)
            {
                TlaMismoQueSmtCheck.IsChecked = true;
            }
        }
        else
        {
            FechaSolicitudPicker.Date = DateTimeOffset.Now;
        }

        NumeroParteSmtBox.LostFocus += NumeroParte_LostFocus;
        NumeroParteBeBox.LostFocus += NumeroParte_LostFocus;
        NumeroParteTlaBox.LostFocus += NumeroParte_LostFocus;

        // Al editar ya viene lleno; se habilita Guardar sin marcar errores
        _listo = true;
        Validar(mostrarErrores: false);
    }

    // Si el número de parte que se acaba de escribir ya pertenece a otro
    // registro (misma pieza, otra área) y el nombre sigue vacío, lo
    // rellena solo — para no tener que volver a escribirlo igual.
    private void NumeroParte_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(NombreModeloBox.Text) || sender is not TextBox box || string.IsNullOrWhiteSpace(box.Text))
        {
            return;
        }

        var numero = box.Text.Trim();
        var coincidencia = _existingRecords.FirstOrDefault(r =>
            r.Id != _editingId &&
            (string.Equals(r.NumeroParteSmt, numero, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(r.NumeroParteBe, numero, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(r.NumeroParteTla, numero, StringComparison.OrdinalIgnoreCase)));

        if (coincidencia is not null)
        {
            NombreModeloBox.Text = coincidencia.NombreModelo;
        }
    }

    // --- "Es el mismo número que SMT" (BE) ---

    private void BeMismoQueSmtCheck_Checked(object sender, RoutedEventArgs e)
    {
        NumeroParteBeBox.Text = NumeroParteSmtBox.Text;
        NumeroParteBeBox.IsEnabled = false;
        NumeroParteSmtBox.TextChanged += SincronizarBeConSmt;
    }

    private void BeMismoQueSmtCheck_Unchecked(object sender, RoutedEventArgs e)
    {
        NumeroParteBeBox.IsEnabled = true;
        NumeroParteSmtBox.TextChanged -= SincronizarBeConSmt;
    }

    private void SincronizarBeConSmt(object sender, TextChangedEventArgs e) => NumeroParteBeBox.Text = NumeroParteSmtBox.Text;

    // --- "Es el mismo número que SMT" (TLA) ---

    private void TlaMismoQueSmtCheck_Checked(object sender, RoutedEventArgs e)
    {
        NumeroParteTlaBox.Text = NumeroParteSmtBox.Text;
        NumeroParteTlaBox.IsEnabled = false;
        NumeroParteSmtBox.TextChanged += SincronizarTlaConSmt;
    }

    private void TlaMismoQueSmtCheck_Unchecked(object sender, RoutedEventArgs e)
    {
        NumeroParteTlaBox.IsEnabled = true;
        NumeroParteSmtBox.TextChanged -= SincronizarTlaConSmt;
    }

    private void SincronizarTlaConSmt(object sender, TextChangedEventArgs e) => NumeroParteTlaBox.Text = NumeroParteSmtBox.Text;

    private int? _duplicadoAvisadoParaId;

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var nombre = NombreModeloBox.Text?.Trim() ?? string.Empty;
        var parteSmt = NumeroParteSmtBox.Text?.Trim() ?? string.Empty;
        var parteBe = NumeroParteBeBox.Text?.Trim() ?? string.Empty;
        var parteTla = NumeroParteTlaBox.Text?.Trim() ?? string.Empty;
        var quienRecibe = QuienRecibeBox.SelectedItem as string ?? string.Empty;

        // El "número de modelo" ya no se captura aparte: se toma el número de
        // parte de mayor prioridad (SMT > BE > TLA) entre los que se llenaron.
        var numero = !string.IsNullOrWhiteSpace(parteSmt) ? parteSmt
            : !string.IsNullOrWhiteSpace(parteBe) ? parteBe
            : parteTla;

        if (!Validar(mostrarErrores: true))
        {
            args.Cancel = true;
            return;
        }

        // No se puede abrir un ContentDialog dentro de otro (WinUI3 solo
        // permite uno a la vez) — en vez de un diálogo de confirmación
        // aparte, se avisa con un InfoBar y se pide un segundo clic en
        // "Guardar" para confirmar que sí se quiere de todos modos.
        var duplicado = EncontrarNumeroParteDuplicado(parteSmt, parteBe, parteTla);
        if (duplicado is not null && duplicado.Id != _duplicadoAvisadoParaId)
        {
            DuplicadoInfoBar.Message = $"\"{duplicado.NumeroModelo} — {duplicado.NombreModelo}\" ya tiene capturado uno de estos números de parte. Dale clic a \"Guardar\" otra vez para guardarlo de todos modos.";
            DuplicadoInfoBar.IsOpen = true;
            _duplicadoAvisadoParaId = duplicado.Id;
            args.Cancel = true;
            return;
        }

        Result = new ModelRecord
        {
            Id = _editingId,
            NumeroModelo = numero,
            NombreModelo = nombre,
            NumeroParteSmt = parteSmt,
            NumeroParteBe = parteBe,
            NumeroParteTla = parteTla,
            QuienRecibeDocumento = quienRecibe,
            FechaSolicitud = FechaSolicitudPicker.Date.Date,
            QuienMandoDocumento = QuienMandaBox.Text?.Trim() ?? string.Empty
        };
    }

    // Busca si alguno de los 3 números de parte ya pertenece a OTRO
    // registro — para avisar antes de crear un duplicado por accidente.
    private ModelRecord? EncontrarNumeroParteDuplicado(string parteSmt, string parteBe, string parteTla)
    {
        bool Coincide(ModelRecord r, string numero) =>
            !string.IsNullOrWhiteSpace(numero) &&
            (string.Equals(r.NumeroParteSmt, numero, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(r.NumeroParteBe, numero, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(r.NumeroParteTla, numero, StringComparison.OrdinalIgnoreCase));

        return _existingRecords.FirstOrDefault(r =>
            r.Id != _editingId && (Coincide(r, parteSmt) || Coincide(r, parteBe) || Coincide(r, parteTla)));
    }

    // --- Validación en vivo ---

    private bool _listo;
    private readonly HashSet<string> _tocados = new();

    private void CampoTexto_Changed(object sender, TextChangedEventArgs e)
    {
        if (sender is FrameworkElement campo)
        {
            _tocados.Add(campo.Name);
        }

        Validar(mostrarErrores: false);
    }

    private void QuienRecibe_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _tocados.Add(QuienRecibeBox.Name);
        Validar(mostrarErrores: false);
    }

    // Habilita Guardar y marca el error solo en los campos que ya se tocaron
    private bool Validar(bool mostrarErrores)
    {
        if (!_listo)
        {
            return false;
        }

        var faltaNombre = string.IsNullOrWhiteSpace(NombreModeloBox.Text);
        var faltaNumero = string.IsNullOrWhiteSpace(NumeroParteSmtBox.Text)
            && string.IsNullOrWhiteSpace(NumeroParteBeBox.Text)
            && string.IsNullOrWhiteSpace(NumeroParteTlaBox.Text);
        var faltaQuien = QuienRecibeBox.SelectedItem is not string;

        var tocoNumero = _tocados.Contains(NumeroParteSmtBox.Name)
            || _tocados.Contains(NumeroParteBeBox.Name)
            || _tocados.Contains(NumeroParteTlaBox.Name);

        MostrarError(NombreErrorText, faltaNombre && (mostrarErrores || _tocados.Contains(NombreModeloBox.Name)), "Escribe el nombre del modelo.");
        MostrarError(NumeroParteErrorText, faltaNumero && (mostrarErrores || tocoNumero), "Captura al menos un número de parte (SMT, BE o TLA).");
        MostrarError(QuienRecibeErrorText, faltaQuien && (mostrarErrores || _tocados.Contains(QuienRecibeBox.Name)), "Elige quién recibe el documento.");

        var valido = !faltaNombre && !faltaNumero && !faltaQuien;
        IsPrimaryButtonEnabled = valido;
        return valido;
    }

    private static void MostrarError(TextBlock texto, bool mostrar, string mensaje)
    {
        texto.Text = mensaje;
        texto.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
    }
}
