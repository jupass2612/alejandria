using System.Collections.Generic;
using System.Linq;
using Alejandria.Models;
using Alejandria.Services;
using Alejandria.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Controls;

// El asistente de 3 pasos para dar de alta un modelo directo en Oracle
// (MODELS_PARENTS / MODELS_MASTER / MODELS_SERIALNUMBERSPECS). Se usa
// incrustado dentro de "Configurar modelo" (con el modelo/área ya
// sabidos — ver InicializarParaModelo) y también solo, desde DataBase
// (ver Inicializar), para trabajar en Oracle sin un modelo de por medio.
public sealed partial class ConfigurarEnImesControl : UserControl
{
    public ConfigurarEnImesViewModel ViewModel { get; } = new();

    private bool _paso1Completo;
    private bool _paso2Completo;
    private bool _paso3Completo;

    // El estatus general de "Configuración Base de Datos — iMES" se
    // calcula directo de estos 3 — ya no depende de leer de vuelta el
    // seguimiento local guardado en SQLite, que se desincronizaba fácil.
    public bool TodosLosPasosCompletos => _paso1Completo && _paso2Completo && _paso3Completo;
    public bool AlgunPasoCompletado => _paso1Completo || _paso2Completo || _paso3Completo;

    // Se dispara cada vez que cambia el estatus de algún paso (o se marca
    // algo en el seguimiento local) — quien lo tenga incrustado (ej. la
    // pastilla de estatus de "Configurar modelo") lo usa para
    // recalcular el estatus general.
    public event EventHandler? SeguimientoLocalActualizado;

    public ConfigurarEnImesControl()
    {
        InitializeComponent();
    }

    // Sin modelo de por medio (uso libre desde DataBase).
    public async void Inicializar()
    {
        ReiniciarVisual();
        ViewModel.Initialize();
        await ViewModel.CargarTodosLosParentsAsync();
    }

    // Con modelo/área ya sabidos — precarga los datos y, al terminar
    // cada paso, marca solo el seguimiento local de ese modelo/área.
    public async void InicializarParaModelo(ModelRecord modelo, string area)
    {
        ReiniciarVisual();
        ViewModel.Initialize(modelo, area);

        // El número de parte puede repetirse entre áreas — para TLA en
        // particular, iMES suele distinguirlo por el nombre, no el
        // número, así que se avisa para que no se confundan al buscar.
        PistaTlaText.Visibility = string.Equals(area, "TLA", System.StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

        SmtBeInfoBar.IsOpen = ViewModel.EsSmtBeCombinado;
        if (ViewModel.EsSmtBeCombinado)
        {
            RefrescarCamposMaster();
        }

        await ViewModel.CargarTodosLosParentsAsync();

        // El modelo padre es del modelo completo — si ya se asignó desde
        // otra área, aparece de una vez como hecho, sin tener que buscarlo
        // ni crearlo otra vez.
        var recordado = await ViewModel.CargarParentRecordadoAsync();
        if (recordado is not null)
        {
            MostrarParentSeleccionado(recordado);

            var masterExistente = await ViewModel.VerificarMasterExistenteAsync();
            if (masterExistente is not null)
            {
                MostrarMasterCompletado(masterExistente, yaExistia: true);
                await VerificarYMostrarSpecsExistentesAsync();
            }
        }

        if (ViewModel.ParentSeleccionado is { } elegido)
        {
            ParentModelIdCombo.SelectedItem = ViewModel.TodosLosParents.FirstOrDefault(p => p.Id == elegido.Id);
        }
    }

    // Regresa el asistente al Paso 1 — hace falta al reutilizar el
    // mismo control para una área distinta (ej. el usuario cambia de
    // SMT a BE en "Configurar modelo" sin recargar toda la página).
    private void ReiniciarVisual()
    {
        Paso1CompletadoPanel.Visibility = Visibility.Collapsed;
        Paso1FormularioPanel.Visibility = Visibility.Visible;
        Paso2CompletadoPanel.Visibility = Visibility.Collapsed;
        Paso2FormularioPanel.Visibility = Visibility.Visible;
        Paso3CompletadoPanel.Visibility = Visibility.Collapsed;
        Paso3FormularioPanel.Visibility = Visibility.Visible;

        _paso1Completo = false;
        _paso2Completo = false;
        _paso3Completo = false;

        MasterBaseText.Visibility = Visibility.Collapsed;
        CopiarSpecsInfoText.Visibility = Visibility.Collapsed;
        CopiarSpecsButton.Visibility = Visibility.Collapsed;
        SeparadorCopiar.Visibility = Visibility.Collapsed;
        EtiquetarSegmentoBorder.Visibility = Visibility.Collapsed;
        TerminadoInfoBar.IsOpen = false;
    }

    // ---- Paso 1: Parent ----

    private async void BuscarParentsButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.BuscarParentsAsync();
        NuevoParentNombreBox.Text = ViewModel.NuevoParent.ParentModelName;
    }

    private async void UsarParentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ImesParentModel parent })
        {
            return;
        }

        var ok = await ViewModel.ElegirParentExistente(parent);
        if (!ok)
        {
            return;
        }

        MostrarParentSeleccionado(parent);

        if (ViewModel.MasterCreado is { } masterExistente)
        {
            MostrarMasterCompletado(masterExistente, yaExistia: true);
            await VerificarYMostrarSpecsExistentesAsync();
        }
    }

    private async void CrearParentButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.NuevoParent.ParentModelName))
        {
            return;
        }

        var ok = await ViewModel.CrearParentNuevoAsync();
        if (ok && ViewModel.ParentSeleccionado is { } creado)
        {
            MostrarParentSeleccionado(creado);
        }
    }

    // Ya existe (elegido, creado, o recordado) — se reemplaza el
    // formulario por la vista de completado, para no poder crear otro
    // por accidente.
    private void MostrarParentSeleccionado(ImesParentModel parent)
    {
        Paso1CompletadoDetalleText.Text = $"\"{parent.ParentModelName}\" (Id {parent.Id})";
        Paso1CompletadoPanel.Visibility = Visibility.Visible;
        Paso1FormularioPanel.Visibility = Visibility.Collapsed;
        _paso1Completo = true;
        SeguimientoLocalActualizado?.Invoke(this, EventArgs.Empty);
    }

    // ---- Paso 2: Master ----

    private async void BuscarMastersButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.BuscarMastersAsync();
    }

    private async void BasarseEnMasterButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ImesMasterModel origen })
        {
            return;
        }

        ViewModel.BasarseEnMasterExistente(origen);
        MasterBaseText.Text = $"Basado en: \"{origen.ModelName}\" ({origen.ModelPartNumber}, Id {origen.Id}). Puedes ajustar lo que haga falta abajo antes de guardar.";
        MasterBaseText.Visibility = Visibility.Visible;
        RefrescarCamposMaster();

        await ViewModel.CargarSpecsDelModeloBaseAsync();
        CopiarSpecsInfoText.Text = $"El modelo base (\"{origen.ModelName}\") tiene {ViewModel.SpecsDelModeloBase.Count} segmento(s) de serial.";
        CopiarSpecsInfoText.Visibility = Visibility.Visible;
        CopiarSpecsButton.Visibility = Visibility.Visible;
        SeparadorCopiar.Visibility = Visibility.Visible;
    }

    private void EmpezarDesdeCeroButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.EmpezarDesdeCero();
        MasterBaseText.Text = "Capturando desde cero (no se basó en ningún modelo existente).";
        MasterBaseText.Visibility = Visibility.Visible;
        RefrescarCamposMaster();

        CopiarSpecsInfoText.Visibility = Visibility.Collapsed;
        CopiarSpecsButton.Visibility = Visibility.Collapsed;
        SeparadorCopiar.Visibility = Visibility.Collapsed;
    }

    // ImesMasterModel es una clase simple sin notificación de cambios —
    // cuando el ViewModel le copia valores por código (basarse en uno
    // existente, o limpiarlo), hay que reflejarlo a mano en los TextBox.
    private void RefrescarCamposMaster()
    {
        var master = ViewModel.NuevoMaster;
        MasterNombreBox.Text = master.ModelName;
        MasterDescBox.Text = master.ModelDesc;
        MasterPartNumberBox.Text = master.ModelPartNumber;
        MasterPartNumber2Box.Text = master.ModelPartNumber2;
        MasterQrBox.Text = master.ModelQrPartNumber;
        MasterProductIdBox.Text = master.ModelProductId;
        MasterFgPartNumberBox.Text = master.FgPartNumber;
        PcbsPerPanelBox.Text = master.PcbsPerPanel?.ToString() ?? string.Empty;
        SimulateInterlockCheck.IsChecked = master.SimulateInterlock;
        ValidateInterlockCheck.IsChecked = master.ValidateInterlock;
        SerialByMesCheck.IsChecked = master.SerialNumberByMes;

        ParentModelIdCombo.SelectedItem = master.ParentModelId.HasValue
            ? ViewModel.TodosLosParents.FirstOrDefault(p => p.Id == master.ParentModelId.Value)
            : null;
    }

    private void ParentModelIdCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.NuevoMaster.ParentModelId = (ParentModelIdCombo.SelectedItem as ImesParentModel)?.Id;
    }

    // Vacío = NULL (sin definir) — no todos los modelos tienen un número
    // fijo de PCBs por panel, así que dejarlo en blanco es una opción
    // válida, no un error.
    private void PcbsPerPanelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.NuevoMaster.PcbsPerPanel = int.TryParse(PcbsPerPanelBox.Text, out var valor) ? valor : null;
    }

    private async void GuardarMasterButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.NuevoMaster.ModelName) || string.IsNullOrWhiteSpace(ViewModel.NuevoMaster.ModelPartNumber))
        {
            return;
        }

        var ok = await ViewModel.GuardarMasterAsync();
        if (ok && ViewModel.MasterCreado is { } creado)
        {
            MostrarMasterCompletado(creado, yaExistia: false);
        }
    }

    // Ya existe (recién guardado, o encontrado en Oracle) — se
    // reemplaza el formulario por la vista de completado.
    private void MostrarMasterCompletado(ImesMasterModel master, bool yaExistia)
    {
        Paso2CompletadoDetalleText.Text = yaExistia
            ? $"Ya existía en iMES: \"{master.ModelName}\" (Id {master.Id})"
            : $"\"{master.ModelName}\" (Id {master.Id})";
        Paso2CompletadoPanel.Visibility = Visibility.Visible;
        Paso2FormularioPanel.Visibility = Visibility.Collapsed;
        _paso2Completo = true;
        SeguimientoLocalActualizado?.Invoke(this, EventArgs.Empty);
    }

    // ---- Paso 3: SerialNumberSpecs — armado visual con clic secuencial ----

    // Si el Master ya tenía segmentos capturados en Oracle, se muestra
    // de una vez como hecho — no tiene caso volver a armarlos.
    private async Task VerificarYMostrarSpecsExistentesAsync()
    {
        var specs = await ViewModel.VerificarSpecsExistentesAsync();
        if (specs.Count > 0)
        {
            Paso3CompletadoDetalleText.Text = $"{specs.Count} segmento(s) ya configurados en iMES.";
            Paso3CompletadoPanel.Visibility = Visibility.Visible;
            Paso3FormularioPanel.Visibility = Visibility.Collapsed;
            _paso3Completo = true;
            SeguimientoLocalActualizado?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CopiarSpecsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CopiarSpecsDelModeloBase();
    }

    private void EmpezarSegmentacionButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.SerialEjemplo))
        {
            return;
        }

        EtiquetarSegmentoBorder.Visibility = Visibility.Collapsed;
        ViewModel.IniciarSegmentacion();
    }

    // Solo llena el campo con un serial sacado del PDF adjunto — no lo
    // prueba contra iMES (eso es aparte, con el botón "Probar" de
    // "Probar un serial").
    private async void ExtraerSerialDelPdfButton_Click(object sender, RoutedEventArgs e)
    {
        var candidatos = await ViewModel.ExtraerCandidatosSerialDelPdfAsync();
        if (candidatos.Count == 0)
        {
            return;
        }

        var elegido = candidatos.Count == 1
            ? candidatos[0].Valor
            : await ElegirSerialDelPdfAsync(candidatos);

        if (elegido is not null)
        {
            // Se pone en los dos lados a propósito: el enlace de doble
            // vía del TextBox no sincroniza sola al ViewModel hasta que
            // el campo pierde el foco, y aquí se llena por código, sin
            // que nadie le haya dado clic — si no se hiciera así,
            // "Empezar" no vería el serial hasta que el usuario le diera
            // clic al campo primero.
            SerialEjemploBox.Text = elegido;
            ViewModel.SerialEjemplo = elegido;
        }
    }

    // El PDF puede traer más de un serial de ejemplo — se muestran todos
    // para que el usuario elija cuál usar.
    private async Task<string?> ElegirSerialDelPdfAsync(List<SerialCandidato> candidatos)
    {
        string? seleccionado = null;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Se encontraron {candidatos.Count} seriales en el PDF adjunto",
            CloseButtonText = "Cancelar"
        };

        var lista = new StackPanel { Spacing = 8, MinWidth = 340 };
        foreach (var candidato in candidatos)
        {
            var boton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(12, 10, 12, 10),
                Content = $"{candidato.EtiquetaMostrada}\n{candidato.Valor}"
            };
            boton.Click += (_, _) =>
            {
                seleccionado = candidato.Valor;
                dialog.Hide();
            };
            lista.Children.Add(boton);
        }

        dialog.Content = new ScrollViewer { Content = lista, MaxHeight = 400 };

        await dialog.ShowAsync();
        return seleccionado;
    }

    // Clic en una casilla = "cierra el segmento aquí" — el sistema ya
    // sabe dónde empieza (justo después del último segmento confirmado).
    private void Celda_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int posicion })
        {
            return;
        }

        var pendiente = ViewModel.ObtenerSegmentoPendiente(posicion);
        if (pendiente is not { } segmento)
        {
            return;
        }

        SegmentoPendienteText.Text = $"Vas a etiquetar las posiciones {segmento.Start}–{posicion} (\"{segmento.Valor}\") — ¿qué es esto?";
        EtiquetaCombo.SelectedItem = null;
        EtiquetaCombo.Text = string.Empty;
        EsDinamicoCheck.IsChecked = false;
        EtiquetarSegmentoBorder.Tag = posicion;
        EtiquetarSegmentoBorder.Visibility = Visibility.Visible;
    }

    private void ConfirmarSegmentoButton_Click(object sender, RoutedEventArgs e)
    {
        if (EtiquetarSegmentoBorder.Tag is not int posicion)
        {
            return;
        }

        var etiqueta = (EtiquetaCombo.SelectedItem as string) ?? EtiquetaCombo.Text;
        if (string.IsNullOrWhiteSpace(etiqueta))
        {
            return;
        }

        ViewModel.ConfirmarSegmento(posicion, etiqueta, EsDinamicoCheck.IsChecked == true);
        EtiquetarSegmentoBorder.Visibility = Visibility.Collapsed;
    }

    private void CancelarSegmentoButton_Click(object sender, RoutedEventArgs e)
    {
        EtiquetarSegmentoBorder.Visibility = Visibility.Collapsed;
    }

    private void QuitarUltimoSegmentoButton_Click(object sender, RoutedEventArgs e)
    {
        EtiquetarSegmentoBorder.Visibility = Visibility.Collapsed;
        ViewModel.QuitarUltimoSegmento();
    }

    private async void GuardarSpecsButton_Click(object sender, RoutedEventArgs e)
    {
        var ok = await ViewModel.GuardarSpecsAsync();
        TerminadoInfoBar.IsOpen = ok;

        if (ok)
        {
            Paso3CompletadoDetalleText.Text = $"{ViewModel.SpecsNuevos.Count} segmento(s) guardados en iMES.";
            Paso3CompletadoPanel.Visibility = Visibility.Visible;
            Paso3FormularioPanel.Visibility = Visibility.Collapsed;
            _paso3Completo = true;
            SeguimientoLocalActualizado?.Invoke(this, EventArgs.Empty);
        }
    }
}
