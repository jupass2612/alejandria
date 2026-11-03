using Microsoft.UI.Xaml;
using Alejandria.Services;

namespace Alejandria;

// Arranque de la app
public partial class App : Application
{
    private Window? _window;

    // La usa FilePickerHelper para sacar el HWND
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }

    // No se marca Handled: mejor que se cierre a que siga en mal estado, pero queda en el log
    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogService.LogError("UnhandledException", e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();
    }
}
