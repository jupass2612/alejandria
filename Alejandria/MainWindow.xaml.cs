using System;
using System.IO;
using System.Linq;
using Alejandria.Models;
using Alejandria.Services;
using Alejandria.ViewModels;
using Alejandria.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Alejandria;

// Ventana principal: barra de título propia, fondo sólido, menú lateral y Frame
public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    private OverlappedPresenter? _presenter;

    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    public MainWindow()
    {
        InitializeComponent();

        Title = ViewModel.AppTitle;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        EnsureSolidBackground();
        SetUpCustomTitleBar();
        ApplySavedTheme();
        LoadAppLogo();

        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastBorder.Visibility = Visibility.Collapsed;
        };
        ToastService.Registrar(MostrarToast);

        ContentFrame.Navigated += ContentFrame_Navigated;

        // Si se navega antes de Activated el Frame puede quedar en blanco
        Activated += MainWindow_Activated;
    }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs e)
    {
        Activated -= MainWindow_Activated;

        try
        {
            var defaultItem = ViewModel.NavItems.FirstOrDefault(item => item.Tag == "Altas");
            if (defaultItem is not null)
            {
                // La navegación real la hace SelectionChanged
                RootNavigationView.SelectedItem = defaultItem;
            }
        }
        catch (Exception)
        {
            // Si falla, mejor dejar la ventana visible que una pantalla negra
        }

        // Sin esperar, para no frenar el arranque si la red está lenta
        _ = RespaldarBaseDeDatosEnSegundoPlanoAsync();

        await MostrarQueHayDeNuevoSiCorrespondeAsync();
    }

    private static async Task RespaldarBaseDeDatosEnSegundoPlanoAsync()
    {
        try
        {
            var settings = new AppSettingsService();
            settings.Load();
            await DatabaseBackupService.RespaldarSiHaceFaltaAsync(settings.DatabasePath);
        }
        catch (Exception ex)
        {
            LogService.LogError("MainWindow.RespaldarBaseDeDatosEnSegundoPlanoAsync", ex);
        }
    }

    // Muestra las notas de la versión una vez si cambió desde la última que vio esta PC.
    // Vacío también cuenta como distinta
    private async Task MostrarQueHayDeNuevoSiCorrespondeAsync()
    {
        try
        {
            var preferences = new UserPreferencesService();
            preferences.Load();

            var versionActual = ChangelogService.VersionActualTexto;
            var versionVista = preferences.LastSeenVersion;

            // Para saber por qué no salió, si vuelve a pasar
            LogService.LogInfo($"Chequeo de novedades — versión actual: \"{versionActual}\", última vista: \"{versionVista}\"");

            if (versionVista == versionActual)
            {
                LogService.LogInfo("No se muestra: la versión actual ya estaba marcada como vista.");
                return;
            }

            var notas = ChangelogService.ObtenerNotasDeVersion(versionActual);

            if (notas is null)
            {
                LogService.LogInfo($"No se muestra: no hay una sección \"## {versionActual}\" en CHANGELOG.md.");
                preferences.MarcarVersionVista(versionActual);
                return;
            }

            // Al primer arranque XamlRoot puede tardar, se espera hasta 2 s
            for (var intentos = 0; intentos < 40 && Content?.XamlRoot is null; intentos++)
            {
                await Task.Delay(50);
            }

            if (Content?.XamlRoot is null)
            {
                LogService.LogInfo("No se muestra: la ventana no tuvo XamlRoot listo tras esperar 2 segundos.");
                // No se marca como vista para intentarlo en el siguiente arranque
                return;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = $"Alejandría se actualizó a la versión {versionActual}",
                CloseButtonText = "Entendido",
                DefaultButton = ContentDialogButton.Close,
                Content = new ScrollViewer
                {
                    MaxHeight = 400,
                    Content = new TextBlock { Text = notas, TextWrapping = TextWrapping.Wrap }
                }
            };

            await dialog.ShowAsync();
            preferences.MarcarVersionVista(versionActual);
        }
        catch (Exception ex)
        {
            // No es crítico, solo queda en el log
            LogService.LogError("MainWindow.MostrarQueHayDeNuevoSiCorrespondeAsync", ex);
        }
    }

    // Quita la barra nativa pero deja el borde para redimensionar
    private void SetUpCustomTitleBar()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            _presenter = presenter;
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        AppWindow.Changed += AppWindow_Changed;
        UpdateMaximizeIcon();
    }

    private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidSizeChange || args.DidPresenterChange)
        {
            UpdateMaximizeIcon();
        }
    }

    private void UpdateMaximizeIcon()
    {
        bool isMaximized = _presenter?.State == OverlappedPresenterState.Maximized;
        MaximizeIcon.Glyph = isMaximized ? "\uE923" : "\uE922";
        ToolTipService.SetToolTip(MaximizeButton, isMaximized ? "Restore" : "Maximize");
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => _presenter?.Minimize();

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_presenter is null)
        {
            return;
        }

        if (_presenter.State == OverlappedPresenterState.Maximized)
        {
            _presenter.Restore();
        }
        else
        {
            _presenter.Maximize();
        }

        UpdateMaximizeIcon();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ApplySavedTheme()
    {
        var preferences = new UserPreferencesService();
        preferences.Load();

        RootGrid.RequestedTheme = preferences.Theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    // Logo por ruta absoluta (ms-appx:// no jala sin empaquetar)
    private void LoadAppLogo()
    {
        try
        {
            var pngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppLogo.png");
            if (File.Exists(pngPath))
            {
                AppLogoImage.Source = new BitmapImage(new Uri(pngPath));
            }

            var icoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(icoPath))
            {
                AppWindow.SetIcon(icoPath);
            }
        }
        catch (Exception)
        {
            // Si no está el logo, sigue sin él
        }
    }

    private void EnsureSolidBackground()
    {
        // Sin Mica/Acrylic: no respeta la paleta y en algunas PCs (VM, Escritorio Remoto) sale negro.
        // El fondo lo pone RootGrid con AppBackgroundBrush
        SystemBackdrop = null;
    }

    private void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        // Usa el PageType de cada NavItem
        var target = ViewModel.NavItems.FirstOrDefault(item => item.Tag == tag)?.PageType ?? typeof(AltasModelosPage);

        if (ContentFrame.CurrentSourcePageType != target)
        {
            ContentFrame.Navigate(target);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // El botón de arriba marca también Ajustes en el menú
        if (RootNavigationView.MenuItemsSource is System.Collections.Generic.IEnumerable<NavItem> items)
        {
            foreach (var item in items)
            {
                if (item.Tag == "Settings")
                {
                    var container = RootNavigationView.ContainerFromMenuItem(item);
                    if (container != null)
                    {
                        RootNavigationView.SelectedItem = container;
                    }
                    break;
                }
            }
        }

        ContentFrame.Navigate(typeof(SettingsPage));
    }

    // Muestra el aviso 3 s; uno nuevo reemplaza al anterior
    private void MostrarToast(string mensaje, ToastService.Tipo tipo)
    {
        ToastIconExito.Visibility = tipo == ToastService.Tipo.Exito ? Visibility.Visible : Visibility.Collapsed;
        ToastIconInfo.Visibility = tipo == ToastService.Tipo.Info ? Visibility.Visible : Visibility.Collapsed;
        ToastIconError.Visibility = tipo == ToastService.Tipo.Error ? Visibility.Visible : Visibility.Collapsed;

        ToastText.Text = mensaje;
        _toastTimer.Stop();
        ToastBorder.Visibility = Visibility.Collapsed;
        ToastBorder.Visibility = Visibility.Visible;
        _toastTimer.Start();
    }

    // Si una página navega a otra del menú, marca la correcta
    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        var item = ViewModel.NavItems.FirstOrDefault(i => i.PageType == e.SourcePageType);
        if (item is not null && !ReferenceEquals(RootNavigationView.SelectedItem, item))
        {
            RootNavigationView.SelectedItem = item;
        }
    }

    // Ctrl+N desde cualquier pantalla: va a Altas y abre Añadir modelo
    private async void NuevoModeloAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (ContentFrame.Content is not AltasModelosPage)
        {
            ContentFrame.Navigate(typeof(AltasModelosPage));
        }

        if (ContentFrame.Content is AltasModelosPage altas)
        {
            await altas.AbrirAñadirModeloAsync();
        }
    }
}
