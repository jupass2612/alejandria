using System.Linq;
using Alejandria.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Views;

public sealed partial class SettingsPage : Page
{
    private readonly UserPreferencesService _preferences = new();
    private readonly AppSettingsService _settings = new();
    private bool _isLoadingInitialState = true;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _preferences.Load();

        // Selecciona el ComboBoxItem cuyo Tag coincide con el tema guardado.
        foreach (var item in ThemeCombo.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag as string == _preferences.Theme)
            {
                ThemeCombo.SelectedItem = item;
                break;
            }
        }

        NotificationsToggle.IsOn = _preferences.NotificationsEnabled;
        BusquedaImesAutomaticaToggle.IsOn = _preferences.BusquedaImesAutomatica;
        _settings.Load();
        VersionInstaladaText.Text = $"Versión instalada: {UpdateService.VersionActualTexto(_settings.UpdatesFolder)}";

        EntornoImesOracleCombo.ItemsSource = _settings.ImesOracleEntornos.Keys.ToList();
        EntornoImesOracleCombo.SelectedItem = _settings.ImesOracleEntornoActivo;

        // A partir de aquí, los eventos SelectionChanged/Toggled ya reflejan
        // cambios reales del usuario, no la carga inicial.
        _isLoadingInitialState = false;
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingInitialState)
        {
            return;
        }

        var theme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Default";
        _preferences.Save(theme, NotificationsToggle.IsOn, BusquedaImesAutomaticaToggle.IsOn);

        if (XamlRoot?.Content is FrameworkElement root)
        {
            root.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private void NotificationsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingInitialState)
        {
            return;
        }

        var theme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Default";
        _preferences.Save(theme, NotificationsToggle.IsOn, BusquedaImesAutomaticaToggle.IsOn);
    }

    private void BusquedaImesAutomaticaToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingInitialState)
        {
            return;
        }

        var theme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Default";
        _preferences.Save(theme, NotificationsToggle.IsOn, BusquedaImesAutomaticaToggle.IsOn);
    }

    private async void BuscarActualizacionButton_Click(object sender, RoutedEventArgs e)
    {
        BuscarActualizacionButton.IsEnabled = false;
        ActualizacionInfoBar.IsOpen = false;

        _settings.Load();
        var resultado = await UpdateService.BuscarActualizacionAsync(_settings.UpdatesFolder);

        if (resultado.Error is not null)
        {
            MostrarResultado(InfoBarSeverity.Warning, resultado.Error);
            BuscarActualizacionButton.IsEnabled = true;
            return;
        }

        var versionInstalada = UpdateService.VersionActualTexto(_settings.UpdatesFolder);

        if (!resultado.HayActualizacion || resultado.Info is null)
        {
            MostrarResultado(InfoBarSeverity.Success, $"Ya tienes la versión más reciente instalada (v{versionInstalada}).");
            BuscarActualizacionButton.IsEnabled = true;
            return;
        }

        var confirmar = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Hay una actualización disponible",
            Content = $"Versión disponible: v{resultado.VersionDisponible}\nVersión instalada: v{versionInstalada}",
            PrimaryButtonText = "Actualizar ahora",
            CloseButtonText = "Más tarde",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await confirmar.ShowAsync() != ContentDialogResult.Primary)
        {
            BuscarActualizacionButton.IsEnabled = true;
            return;
        }

        MostrarResultado(InfoBarSeverity.Informational, "Descargando la actualización...");

        var (exito, error) = await UpdateService.DescargarActualizacionAsync(_settings.UpdatesFolder, resultado.Info);

        if (!exito)
        {
            MostrarResultado(InfoBarSeverity.Warning, error ?? "No se pudo descargar la actualización.");
            BuscarActualizacionButton.IsEnabled = true;
            return;
        }

        var reiniciar = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Actualización lista",
            Content = $"Se descargó la versión v{resultado.VersionDisponible}. Reinicia Alejandría para aplicarla.",
            PrimaryButtonText = "Reiniciar ahora",
            CloseButtonText = "Más tarde",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await reiniciar.ShowAsync() == ContentDialogResult.Primary)
        {
            UpdateService.InstalarYReiniciar(_settings.UpdatesFolder, resultado.Info);
            return;
        }

        BuscarActualizacionButton.IsEnabled = true;
    }

    private async void VerNotasVersionButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Notas de versión (actual: v{ChangelogService.VersionActualTexto})",
            CloseButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Close,
            Content = new ScrollViewer
            {
                MaxHeight = 500,
                Content = new TextBlock
                {
                    Text = ChangelogService.ObtenerChangelogCompleto(),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        await dialog.ShowAsync();
    }

    private async void VerRegistroErroresButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Registro de errores (últimos 5 días)",
            CloseButtonText = "Cerrar",
            DefaultButton = ContentDialogButton.Close,
            Content = new ScrollViewer
            {
                MaxHeight = 500,
                Content = new TextBlock
                {
                    Text = LogService.LeerRecientes(),
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                }
            }
        };

        await dialog.ShowAsync();
    }

    private void AbrirCarpetaLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            FileOpenService.AbrirCarpeta(LogService.CarpetaLogsRuta);
        }
        catch (Exception ex)
        {
            MostrarResultado(InfoBarSeverity.Warning, $"No se pudo abrir la carpeta de registros: {LogService.Redactar(ex.Message)}");
        }
    }

    private void MostrarResultado(InfoBarSeverity severidad, string mensaje)
    {
        ActualizacionInfoBar.Severity = severidad;
        ActualizacionInfoBar.Message = mensaje;
        ActualizacionInfoBar.IsOpen = true;
    }

    private void EntornoImesOracleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingInitialState || EntornoImesOracleCombo.SelectedItem is not string entorno)
        {
            return;
        }

        _settings.GuardarEntornoImesOracleActivo(entorno);
        ConexionOracleInfoBar.IsOpen = false;
    }

    private async void ProbarConexionOracleButton_Click(object sender, RoutedEventArgs e)
    {
        var entorno = _settings.ImesOracleEntornoSeleccionado;
        if (entorno is null)
        {
            ConexionOracleInfoBar.Severity = InfoBarSeverity.Warning;
            ConexionOracleInfoBar.Message = "No hay un entorno seleccionado.";
            ConexionOracleInfoBar.IsOpen = true;
            return;
        }

        ProbarConexionOracleButton.IsEnabled = false;
        ProbarConexionOracleRing.IsActive = true;
        ConexionOracleInfoBar.IsOpen = false;

        var connectionService = new ImesOracleConnectionService(entorno);
        var (exito, error) = await connectionService.ProbarConexionAsync();

        if (!exito)
        {
            ConexionOracleInfoBar.Severity = InfoBarSeverity.Error;
            ConexionOracleInfoBar.Message = $"No se pudo conectar ({_settings.ImesOracleEntornoActivo}): {error}";
            ConexionOracleInfoBar.IsOpen = true;
            ProbarConexionOracleButton.IsEnabled = true;
            ProbarConexionOracleRing.IsActive = false;
            return;
        }

        var (esquemaOk, errorEsquema, parents, master, serialSpecs) = await connectionService.ProbarEsquemaAsync();

        ConexionOracleInfoBar.Severity = esquemaOk ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ConexionOracleInfoBar.Message = esquemaOk
            ? $"Conexión exitosa ({_settings.ImesOracleEntornoActivo}). MODELS_PARENTS: {parents}, MODELS_MASTER: {master}, MODELS_SERIALNUMBERSPECS: {serialSpecs} filas."
            : $"La conexión funcionó, pero no se pudieron contar las tablas: {errorEsquema}";
        ConexionOracleInfoBar.IsOpen = true;

        ProbarConexionOracleButton.IsEnabled = true;
        ProbarConexionOracleRing.IsActive = false;
    }
}
