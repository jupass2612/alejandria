using Alejandria.Models;
using Alejandria.Services;
using Alejandria.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;

namespace Alejandria.Views;

public sealed partial class ModelConfigPage : Page
{
    public ModelConfigViewModel ViewModel { get; } = new();
    private readonly UserPreferencesService _preferences = new();
    private bool _isSettingAreaProgrammatically;
    private bool _isPopulatingFields;

    // Autoguardado 2 s después del último cambio
    private readonly DispatcherTimer _autoguardadoTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    // Avance automático al completar un paso
    private readonly DispatcherTimer _avanceTimer = new() { Interval = TimeSpan.FromMilliseconds(1500) };
    private int _pasoPorAvanzar;

    public ModelConfigPage()
    {
        InitializeComponent();
        _autoguardadoTimer.Tick += AutoguardadoTimer_Tick;
        _avanceTimer.Tick += AvanceTimer_Tick;
    }

    private void ReiniciarAutoguardado()
    {
        AutoguardadoText.Visibility = Visibility.Collapsed;
        _autoguardadoTimer.Stop();
        _autoguardadoTimer.Start();
    }

    private async void AutoguardadoTimer_Tick(object? sender, object e)
    {
        _autoguardadoTimer.Stop();

        var ok = await ViewModel.SaveAsync();
        if (ok)
        {
            AutoguardadoText.Visibility = Visibility.Visible;
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not ModelRecord modelo)
        {
            return;
        }

        _preferences.Load();

        HeaderSubtitleText.Text = $"{modelo.NumeroModelo} · {modelo.NombreModelo}";

        await ViewModel.InitializeAsync(modelo);

        _isSettingAreaProgrammatically = true;
        AreaCombo.ItemsSource = ViewModel.AreasDisponibles;
        AreaCombo.SelectedItem = ViewModel.AreaSeleccionada;
        _isSettingAreaProgrammatically = false;

        PopulateFieldsFromDetalle();
        await BuscarEnImesAutomaticoSiAplicaAsync();
    }

    private async void AreaCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSettingAreaProgrammatically)
        {
            return;
        }

        if (AreaCombo.SelectedItem is not string area)
        {
            return;
        }

        _avanceTimer.Stop();
        await ViewModel.CargarAreaAsync(area);
        PopulateFieldsFromDetalle();
        await BuscarEnImesAutomaticoSiAplicaAsync();
    }

    // Busca sola en iMES si está prendido y no hay datos; si no encuentra, solo avisa
    private async Task BuscarEnImesAutomaticoSiAplicaAsync()
    {
        if (!_preferences.BusquedaImesAutomatica)
        {
            return;
        }

        var detalle = ViewModel.Detalle;
        var yaTieneAlgoCapturado = !string.IsNullOrWhiteSpace(detalle.WorkOrder)
            || !string.IsNullOrWhiteSpace(detalle.Ruta)
            || !string.IsNullOrWhiteSpace(detalle.Linea);

        if (yaTieneAlgoCapturado)
        {
            return;
        }

        await ViewModel.BuscarEnImesAsync();
        AplicarResultadosImes();
    }

    // Pasa a pantalla lo que encontró iMES (si hay varios WO se elige en el combo)
    private void AplicarResultadosImes()
    {
        if (ViewModel.OpcionesWoPrincipal.Count == 0 && ViewModel.OpcionesWoBe.Count == 0)
        {
            return;
        }

        ViewModel.AplicarResultadosImes();
        PopulateFieldsFromDetalle();
        ToastService.Mostrar(ViewModel.SuccessMessage, ToastService.Tipo.Info);
    }

    private void WoPrincipalCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPopulatingFields || WoPrincipalCombo.SelectedItem is not ImesWorkOrder wo)
        {
            return;
        }

        ViewModel.AplicarWoPrincipal(wo);
        PopulateFieldsFromDetalle();
        ReiniciarAutoguardado();
    }

    private void WoBeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPopulatingFields || WoBeCombo.SelectedItem is not ImesWorkOrder wo)
        {
            return;
        }

        ViewModel.AplicarWoBe(wo);
        PopulateFieldsFromDetalle();
        ReiniciarAutoguardado();
    }

    // Pasa el detalle a los controles a mano para controlar los recálculos
    private void PopulateFieldsFromDetalle()
    {
        _isPopulatingFields = true;
        var detalle = ViewModel.Detalle;

        ParentsCombo.SelectedItem = NullIfEmpty(detalle.ImesModelsParents);
        MasterCombo.SelectedItem = NullIfEmpty(detalle.ImesModelsMaster);
        SerialSpecsCombo.SelectedItem = NullIfEmpty(detalle.ImesModelsSerialNumberSpecs);
        PartLineCombo.SelectedItem = NullIfEmpty(detalle.PartLineBomMaintenance);

        WorkOrderBox.Text = detalle.WorkOrder;
        RutaBox.Text = detalle.Ruta;
        FlujoBox.Text = detalle.Flujo;
        LineaBox.Text = detalle.Linea;
        WorkOrderBeBox.Text = detalle.WorkOrderBe;
        FlujoBeBox.Text = detalle.Flujo;
        LineaBeBox.Text = detalle.LineaBe;

        // Los combos marcan el WO que quedó, si viene de la lista
        WoPrincipalCombo.SelectedItem = ViewModel.OpcionesWoPrincipal.FirstOrDefault(o => o.WorkOrder == detalle.WorkOrder && o.Line == detalle.Linea);
        WoBeCombo.SelectedItem = ViewModel.OpcionesWoBe.FirstOrDefault(o => o.WorkOrder == detalle.WorkOrderBe && o.Line == detalle.LineaBe);

        EstadoDocumentoCombo.SelectedItem = NullIfEmpty(detalle.EstadoDocumento) ?? ConfigStatusHelper.DocumentoCorrecto;
        _isPopulatingFields = false;
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private void StatusField_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo)
        {
            return;
        }

        var value = combo.SelectedItem as string ?? string.Empty;

        switch (combo.Name)
        {
            case nameof(ParentsCombo): ViewModel.Detalle.ImesModelsParents = value; break;
            case nameof(MasterCombo): ViewModel.Detalle.ImesModelsMaster = value; break;
            case nameof(SerialSpecsCombo): ViewModel.Detalle.ImesModelsSerialNumberSpecs = value; break;
            case nameof(PartLineCombo): ViewModel.Detalle.PartLineBomMaintenance = value; break;
            case nameof(EstadoDocumentoCombo): ViewModel.Detalle.EstadoDocumento = value; break;
        }

        ViewModel.RecomputeStatuses();

        if (!_isPopulatingFields)
        {
            ReiniciarAutoguardado();
            ProgramarAvanceSiSeCompleto(combo.Name);
        }
    }

    // Si el combo completó su paso, programa el avance
    private void ProgramarAvanceSiSeCompleto(string nombreCombo)
    {
        _avanceTimer.Stop();

        if (!_preferences.AvanceAutomaticoPasos)
        {
            return;
        }

        var paso = nombreCombo switch
        {
            nameof(ParentsCombo) => 1,
            nameof(MasterCombo) => 2,
            nameof(SerialSpecsCombo) => 3,
            _ => 0
        };

        var estatus = paso switch
        {
            1 => ViewModel.EstatusPaso1,
            2 => ViewModel.EstatusPaso2,
            3 => ViewModel.EstatusPaso3,
            _ => string.Empty
        };

        if (paso == 0 || paso != ViewModel.PasoActual || estatus != ConfigStatusHelper.Terminado)
        {
            return;
        }

        _pasoPorAvanzar = paso;
        _avanceTimer.Start();
    }

    private void AvanceTimer_Tick(object? sender, object e)
    {
        _avanceTimer.Stop();

        // Solo si no se movió a mano
        if (ViewModel.PasoActual == _pasoPorAvanzar)
        {
            IrAPaso(_pasoPorAvanzar + 1);
        }
    }

    private void ModelIdBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Al cargar también cambia, ahí no se guarda
        if (!ViewModel.IsLoading && !_isPopulatingFields)
        {
            ReiniciarAutoguardado();
        }
    }

    private void TextField_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        switch (box.Name)
        {
            case nameof(WorkOrderBox): ViewModel.Detalle.WorkOrder = box.Text; break;
            case nameof(RutaBox): ViewModel.Detalle.Ruta = box.Text; break;
            case nameof(FlujoBox): ViewModel.Detalle.Flujo = box.Text; break;
            case nameof(LineaBox): ViewModel.Detalle.Linea = box.Text; break;
            case nameof(WorkOrderBeBox): ViewModel.Detalle.WorkOrderBe = box.Text; break;
            case nameof(FlujoBeBox): ViewModel.Detalle.Flujo = box.Text; break;
            case nameof(LineaBeBox): ViewModel.Detalle.LineaBe = box.Text; break;
        }

        ViewModel.RecomputeStatuses();

        if (!_isPopulatingFields)
        {
            ReiniciarAutoguardado();
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await GuardarConfiguracionAsync();
    }

    // Enter guarda, menos si se está escribiendo en un TextBox
    private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter || e.OriginalSource is TextBox)
        {
            return;
        }

        e.Handled = true;
        await GuardarConfiguracionAsync();
    }

    // Ctrl+S guarda
    private async void GuardarAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await GuardarConfiguracionAsync();
    }

    // Alt+← y Alt+→ entre pasos
    private void PasoAnteriorAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        IrAPaso(ViewModel.PasoActual - 1);
    }

    private void PasoSiguienteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        IrAPaso(ViewModel.PasoActual + 1);
    }

    private async Task GuardarConfiguracionAsync()
    {
        _autoguardadoTimer.Stop();
        AutoguardadoText.Visibility = Visibility.Collapsed;
        var ok = await ViewModel.SaveAsync();

        var preferences = new UserPreferencesService();
        preferences.Load();

        if (ok && preferences.NotificationsEnabled)
        {
            ToastService.Mostrar(ViewModel.SuccessMessage);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _autoguardadoTimer.Stop();
        _avanceTimer.Stop();
    }

    private async void CopiarConfiguracionButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.AreaConfigCopiable is not { } areaOrigen)
        {
            return;
        }

        var ok = await ViewModel.CopiarConfiguracionDeAsync(areaOrigen);
        if (ok)
        {
            PopulateFieldsFromDetalle();
            ToastService.Mostrar(ViewModel.SuccessMessage);
        }
    }

    private async void BuscarEnImesButton_Click(object sender, RoutedEventArgs e)
    {
        // Clic manual: sin caché
        ImesApiService.LimpiarCache();
        await BuscarEnImesConReintentoAsync();
    }

    // Si no encuentra, pregunta si reintentar o capturar a mano
    private async Task BuscarEnImesConReintentoAsync()
    {
        var todoEncontrado = await ViewModel.BuscarEnImesAsync();
        AplicarResultadosImes();

        if (todoEncontrado)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = ViewModel.OpcionesWoPrincipal.Count > 0 || ViewModel.OpcionesWoBe.Count > 0
                ? "Falta un work order en iMES"
                : "No se encontró nada en iMES",
            Content = ViewModel.ErrorMessage ?? "No se encontró ningún Work Order para este número de parte.",
            PrimaryButtonText = "Ingresar manual",
            SecondaryButtonText = "Volver a intentar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Secondary
        };

        var resultado = await dialog.ShowAsync();

        if (resultado == ContentDialogResult.Primary)
        {
            ViewModel.PermiteCapturaManualPlataforma = true;
        }
        else if (resultado == ContentDialogResult.Secondary)
        {
            await BuscarEnImesConReintentoAsync();
        }
    }

    private async void ProbarSerialButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ProbarSerialAsync();

        // Severity no se enlaza desde un bool, se pone aquí
        ResultadoSerialInfoBar.Severity = ViewModel.ResultadoSerialEncontrado
            ? InfoBarSeverity.Success
            : InfoBarSeverity.Warning;
    }

    // Solo llena el serial del PDF, no lo prueba
    private async void ExtraerSerialDelPdfButton_Click(object sender, RoutedEventArgs e)
    {
        var candidatos = ViewModel.ExtraerCandidatosSerialDelPdf();
        if (candidatos.Count == 0)
        {
            return;
        }

        var elegido = candidatos.Count == 1
            ? candidatos[0].Valor
            : await ElegirSerialDelPdfAsync(candidatos);

        if (elegido is not null)
        {
            // Directo al ViewModel para no depender del foco
            ViewModel.SerialPrueba = elegido;
        }
    }

    // Si hay varios seriales se elige uno
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

    private void PlantillaSpecsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.PlantillaSeleccionada = PlantillaSpecsCombo.SelectedItem as SerialSpecPlantilla;
    }

    private void CopiarSqlSpecsButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.SqlSpecs))
        {
            return;
        }

        var paquete = new DataPackage();
        paquete.SetText(ViewModel.SqlSpecs);
        Clipboard.SetContent(paquete);

        ToastService.Mostrar("SQL copiado. Revísalo antes de correrlo en iMES.");
    }

    private void PasoButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out var paso))
        {
            IrAPaso(paso);
        }
    }

    private void AnteriorButton_Click(object sender, RoutedEventArgs e) => IrAPaso(ViewModel.PasoActual - 1);

    private void SiguienteButton_Click(object sender, RoutedEventArgs e) => IrAPaso(ViewModel.PasoActual + 1);

    // Cambia de paso y sube el scroll
    private void IrAPaso(int paso)
    {
        _avanceTimer.Stop();
        ViewModel.IrAPaso(paso);
        PasosScroll.ChangeView(null, 0, null);
    }
}
