using Microsoft.UI.Xaml;
using Alejandria.Services;

namespace Alejandria;

// Application entry point.
public partial class App : Application
{
    private Window? _window;

    // Referencia a la ventana principal — FilePickerHelper la usa para
    // el HWND que necesita FileSavePicker.
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += App_UnhandledException;
    }

    // No se marca e.Handled = true a propósito: si de verdad no se pudo
    // manejar en su lugar, es más seguro dejar que la app se cierre que
    // seguir en un estado que ya no se sabe si quedó bien — pero al menos
    // queda registrado para poder revisarlo después.
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
