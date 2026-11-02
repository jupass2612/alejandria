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

// App shell: custom title bar, solid background (no Mica/Acrylic), left
// NavigationView, and a content Frame that hosts each page.
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

        // Navegar aquí mismo (antes de Activate()) puede dejar el Frame en
        // blanco, porque la ventana todavía no está "viva". Se espera a
        // Activated y se hace una sola vez.
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
                // Selecciona el item; RootNavigationView_SelectionChanged hace la navegación real,
                // así queda un único punto de entrada para navegar.
                RootNavigationView.SelectedItem = defaultItem;
            }
        }
        catch (Exception)
        {
            // Si algo falla aquí, es mejor dejar la ventana visible (aunque
            // sin navegar) que arriesgar una pantalla negra silenciosa.
        }

        // Se dispara sin esperar — un respaldo tardado o fallido (ej. red
        // lenta) no debe retrasar que la ventana termine de arrancar.
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

    // Compara la versión actual contra la última que ya vio esta
    // computadora (guardada en preferences.json) — si es distinta,
    // muestra las notas de la versión actual una sola vez. Un
    // LastSeenVersion vacío se trata igual que "distinto" (no como
    // "instalación nueva, no mostrar nada"): si esta función se agregó
    // en una versión posterior a la que ya tenían instalada, el primer
    // arranque con el código nuevo tendría LastSeenVersion vacío aunque
    // sí sea una actualización real — así también se muestra ahí.
    private async Task MostrarQueHayDeNuevoSiCorrespondeAsync()
    {
        try
        {
            var preferences = new UserPreferencesService();
            preferences.Load();

            var versionActual = ChangelogService.VersionActualTexto;
            var versionVista = preferences.LastSeenVersion;

            // Diagnóstico temporal: mientras se confirma que esto ya
            // funciona bien en un caso real, se deja registrado qué
            // valores está comparando cada vez que arranca — así, si
            // vuelve a no salir, el log dice por qué en vez de adivinar.
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

            // Justo después de Activated, en el primer arranque (más
            // notorio recién tras actualizar) XamlRoot puede tardar unos
            // milisegundos más en estar listo — se espera hasta 2
            // segundos en vez de rendirse al primer intento.
            for (var intentos = 0; intentos < 40 && Content?.XamlRoot is null; intentos++)
            {
                await Task.Delay(50);
            }

            if (Content?.XamlRoot is null)
            {
                LogService.LogInfo("No se muestra: la ventana no tuvo XamlRoot listo tras esperar 2 segundos.");
                // No se marca como vista — así se vuelve a intentar en el
                // siguiente arranque, en vez de perder la oportunidad.
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
            // No es crítico — si algo falla aquí, la app sigue funcionando
            // normal — pero se registra, para poder ver por qué si vuelve a pasar.
            LogService.LogError("MainWindow.MostrarQueHayDeNuevoSiCorrespondeAsync", ex);
        }
    }

    // Removes the native title bar but keeps the resizable border. Dragging
    // and the caption buttons in MainWindow.xaml take over from here.
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

    // Carga el logo por ruta absoluta (ms-appx:// no funciona sin empaquetar).
    // Se usa para el ícono de la barra de título y de la ventana.
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
            // El logo es cosmético: si el archivo no está (por ejemplo, en un
            // build que no copió Assets), la app sigue funcionando sin él.
        }
    }

    private void EnsureSolidBackground()
    {
        // Deliberadamente sin Mica/Acrylic: el estándar de diseño del
        // proyecto prohíbe explícitamente efectos "glassmorphism", y un
        // backdrop translúcido de todos modos ignora nuestra paleta
        // personalizada (se pinta con el tinte que decida Windows, no con
        // AppBackgroundBrush) — en algunas máquinas (VMs, Escritorio
        // Remoto, DWM limitado) además renderiza negro sólido. El fondo
        // real ahora es 100% sólido: RootGrid.Background en MainWindow.xaml,
        // usando AppBackgroundBrush del estándar de diseño.
        SystemBackdrop = null;
    }

    private void RootNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        // Usa el PageType de cada NavItem, una sola fuente de verdad
        var target = ViewModel.NavItems.FirstOrDefault(item => item.Tag == tag)?.PageType ?? typeof(AltasModelosPage);

        if (ContentFrame.CurrentSourcePageType != target)
        {
            ContentFrame.Navigate(target);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // Mirrors the sidebar's Settings item so the top bar shortcut also works.
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

    // Muestra el aviso abajo 3 segundos; uno nuevo reemplaza al anterior
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

    // Si una página navega a otra del menú, el menú marca la correcta
    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        var item = ViewModel.NavItems.FirstOrDefault(i => i.PageType == e.SourcePageType);
        if (item is not null && !ReferenceEquals(RootNavigationView.SelectedItem, item))
        {
            RootNavigationView.SelectedItem = item;
        }
    }

    // Ctrl+N desde cualquier pantalla: va a Altas y abre "Añadir modelo"
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
