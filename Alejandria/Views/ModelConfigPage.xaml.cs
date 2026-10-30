using Alejandria.Models;
using Alejandria.Services;
using Alejandria.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace Alejandria.Views;

public sealed partial class ModelConfigPage : Page
{
    public ModelConfigViewModel ViewModel { get; } = new();
    private readonly UserPreferencesService _preferences = new();
    private bool _isSettingAreaProgrammatically;
    private bool _isPopulatingFields;

    // Guarda solo, 2 segundos después del último cambio — para no
    // depender de acordarse del botón. Se reinicia con cada campo que
    // cambie; "Guardar configuración" sigue ahí para quien prefiera
    // guardar de una vez.
    private readonly DispatcherTimer _autoguardadoTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    public ModelConfigPage()
    {
        InitializeComponent();
        _autoguardadoTimer.Tick += AutoguardadoTimer_Tick;

        // El asistente de Oracle escribe el seguimiento local directo a
        // la base de datos (no al Detalle que ya está en memoria aquí) —
        // sin esto, las pastillas de arriba se quedan mostrando el
        // estatus viejo aunque el asistente ya diga "completado".
        OracleWizard.SeguimientoLocalActualizado += OracleWizard_SeguimientoLocalActualizado;
    }

    private async void OracleWizard_SeguimientoLocalActualizado(object? sender, EventArgs e)
    {
        // El estatus de la pastilla de arriba ya no depende de volver a
        // leer SQLite — se calcula directo de los 3 pasos del asistente,
        // que es la fuente de verdad real.
        EstatusBdImesBadge.Status = OracleWizard.TodosLosPasosCompletos
            ? ConfigStatusHelper.Terminado
            : OracleWizard.AlgunPasoCompletado
                ? ConfigStatusHelper.EnProceso
                : ConfigStatusHelper.Pendiente;

        // El seguimiento local (SQLite) sigue actualizándose para que
        // Histórico/Estadísticas/reportes sigan funcionando — se vuelve a
        // leer aquí para resincronizar los demás campos de la pantalla,
        // sin tocar el asistente, para no reiniciarlo a medio uso.
        await ViewModel.CargarAreaAsync(ViewModel.AreaSeleccionada);
        PopulateFieldsFromDetalleSinReiniciarWizard();
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

        SuccessInfoBar.IsOpen = false;
        await ViewModel.CargarAreaAsync(area);
        PopulateFieldsFromDetalle();
        await BuscarEnImesAutomaticoSiAplicaAsync();
    }

    // Si está prendido en Ajustes y el área todavía no tiene Work
    // Order/Ruta/Línea capturados, busca sola en iMES sin que haga falta
    // darle clic — pero si no encuentra nada, no muestra el diálogo de
    // "reintentar / ingresar manual" (eso queda para cuando el usuario
    // le da clic él mismo a "Buscar en iMES"), solo el aviso normal.
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

        var candidatos = await ViewModel.BuscarEnImesAsync();
        if (candidatos.Count > 0)
        {
            ViewModel.AplicarWorkOrdersImes(candidatos);
            PopulateFieldsFromDetalle();
            SuccessInfoBar.IsOpen = true;
        }
    }

    // Refleja los valores cargados en los controles. Es manual (no binding)
    // para controlar cuándo se disparan los recálculos de estatus.
    private void PopulateFieldsFromDetalle()
    {
        _isPopulatingFields = true;
        var detalle = ViewModel.Detalle;

        PartLineCombo.SelectedItem = NullIfEmpty(detalle.PartLineBomMaintenance);

        WorkOrderBox.Text = detalle.WorkOrder;
        RutaBox.Text = detalle.Ruta;
        FlujoBox.Text = detalle.Flujo;
        LineaBox.Text = detalle.Linea;

        EstadoDocumentoCombo.SelectedItem = NullIfEmpty(detalle.EstadoDocumento) ?? ConfigStatusHelper.DocumentoCorrecto;
        _isPopulatingFields = false;

        // El asistente de Oracle se reinicia cada vez que se (re)carga
        // el área — así siempre arranca en el Paso 1 para la área
        // correcta, en vez de arrastrar el estado de la anterior.
        OracleWizard.InicializarParaModelo(ViewModel.Modelo, ViewModel.AreaSeleccionada);
    }

    // Igual que PopulateFieldsFromDetalle, pero sin reiniciar el
    // asistente de Oracle — para cuando el asistente mismo es quien
    // avisó que algo cambió (reiniciarlo ahí perdería en qué paso
    // estaba y repetiría consultas a Oracle sin necesidad).
    private void PopulateFieldsFromDetalleSinReiniciarWizard()
    {
        _isPopulatingFields = true;
        var detalle = ViewModel.Detalle;

        PartLineCombo.SelectedItem = NullIfEmpty(detalle.PartLineBomMaintenance);

        WorkOrderBox.Text = detalle.WorkOrder;
        RutaBox.Text = detalle.Ruta;
        FlujoBox.Text = detalle.Flujo;
        LineaBox.Text = detalle.Linea;

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
            case nameof(PartLineCombo): ViewModel.Detalle.PartLineBomMaintenance = value; break;
            case nameof(EstadoDocumentoCombo): ViewModel.Detalle.EstadoDocumento = value; break;
        }

        ViewModel.RecomputeStatuses();

        if (!_isPopulatingFields)
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

    // Enter guarda igual que el botón — para no tener que ir a buscarlo
    // con el mouse cuando se configuran varios modelos seguidos.
    private async void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await GuardarConfiguracionAsync();
    }

    private async Task GuardarConfiguracionAsync()
    {
        _autoguardadoTimer.Stop();
        AutoguardadoText.Visibility = Visibility.Collapsed;
        SuccessInfoBar.IsOpen = false;
        var ok = await ViewModel.SaveAsync();

        var preferences = new UserPreferencesService();
        preferences.Load();

        SuccessInfoBar.IsOpen = ok && preferences.NotificationsEnabled;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _autoguardadoTimer.Stop();
    }

    private async void CopiarConfiguracionButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.AreaConfigCopiable is not { } areaOrigen)
        {
            return;
        }

        SuccessInfoBar.IsOpen = false;
        var ok = await ViewModel.CopiarConfiguracionDeAsync(areaOrigen);
        if (ok)
        {
            PopulateFieldsFromDetalle();
            SuccessInfoBar.IsOpen = true;
        }
    }

    private async void BuscarEnImesButton_Click(object sender, RoutedEventArgs e)
    {
        SuccessInfoBar.IsOpen = false;

        // Clic manual = quiere ver el dato más reciente, no lo que quedó
        // en caché de una consulta automática de hace un momento.
        ImesApiService.LimpiarCache();
        await BuscarEnImesConReintentoAsync();
    }

    // Busca en iMES; si no encuentra nada, pregunta si reintentar o
    // capturar a mano (solo ahí se destraban los campos) en vez de
    // destrabarlos solo. "Volver a intentar" repite esto mismo.
    private async Task BuscarEnImesConReintentoAsync()
    {
        var candidatos = await ViewModel.BuscarEnImesAsync();

        if (candidatos.Count > 0)
        {
            // Con el estándar nuevo de nombres de Work Order, varias líneas
            // físicas de la misma área comparten el mismo Work Order — así
            // que ya no hay que elegir una, se juntan todas (ver AplicarWorkOrdersImes).
            ViewModel.AplicarWorkOrdersImes(candidatos);
            PopulateFieldsFromDetalle();
            SuccessInfoBar.IsOpen = true;
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "No se encontró nada en iMES",
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
        // Ya no se escribe a mano — siempre se prueba el mismo serial de
        // ejemplo que se capturó en el Paso 3 de arriba.
        ViewModel.SerialPrueba = OracleWizard.ViewModel.SerialEjemplo;

        await ViewModel.ProbarSerialAsync();

        // El Severity no se puede enlazar directo desde un bool sin un
        // converter propio para esto solo — más simple ponerlo aquí justo
        // después de que la respuesta ya está lista.
        ResultadoSerialInfoBar.Severity = ViewModel.ResultadoSerialEncontrado
            ? InfoBarSeverity.Success
            : InfoBarSeverity.Warning;
    }
}
