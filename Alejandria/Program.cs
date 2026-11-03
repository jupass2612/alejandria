using Microsoft.UI.Xaml;
using Velopack;

namespace Alejandria;

// Main propio para correr VelopackApp antes de la UI (necesita DISABLE_XAML_GENERATED_MAIN).
// También evita abrir la app dos veces con un Mutex global
public static class Program
{
    private const string NombreMutexInstanciaUnica = @"Global\Alejandria_BCS_AIS_SingleInstance";

    [global::System.Runtime.InteropServices.DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    [global::System.Runtime.InteropServices.DllImport("user32.dll", CharSet = global::System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string texto, string titulo, uint tipo);

    private const uint MB_ICONINFORMATION = 0x40;

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        // El Mutex se libera hasta que se cierra la app
        System.Threading.Mutex mutexInstanciaUnica;
        bool esInstanciaNueva;

        try
        {
            mutexInstanciaUnica = new System.Threading.Mutex(true, NombreMutexInstanciaUnica, out esInstanciaNueva);
        }
        catch (UnauthorizedAccessException)
        {
            // Sin permiso para el Mutex global (pasa en Escritorio Remoto), se usa uno de sesión
            mutexInstanciaUnica = new System.Threading.Mutex(true, NombreMutexInstanciaUnica.Replace(@"Global\", string.Empty), out esInstanciaNueva);
        }

        using var _ = mutexInstanciaUnica;

        if (!esInstanciaNueva)
        {
            MessageBox(IntPtr.Zero, "Alejandría ya está abierta en esta computadora.", "Alejandría", MB_ICONINFORMATION);
            return;
        }

        // Errores de otros hilos y tareas sin await que no llegan a App.xaml.cs
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Services.LogService.LogError("AppDomain.UnhandledException", ex);
            }
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Services.LogService.LogError("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        XamlCheckProcessRequirements();
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var contexto = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            System.Threading.SynchronizationContext.SetSynchronizationContext(contexto);
            new App();
        });
    }
}
