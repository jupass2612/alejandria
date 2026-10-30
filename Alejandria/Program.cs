using Microsoft.UI.Xaml;
using Velopack;

namespace Alejandria;

// WinUI 3 normalmente genera su propio Main — hace falta uno propio para
// poder correr VelopackApp.Build().Run() antes de que arranque la UI (así
// es como Velopack detecta si lo están abriendo por un instalar/actualizar/
// desinstalar en vez de un uso normal). Requiere DISABLE_XAML_GENERATED_MAIN
// en el .csproj para que el generador no cree otro Main que choque con este.
//
// También aquí se controla que no se pueda abrir una segunda instancia:
// un Mutex con nombre fijo ("Global\" para que aplique a toda la
// computadora, no solo a la sesión de Windows actual) — la primera
// instancia lo toma y lo mantiene mientras esté abierta; si otra
// instancia lo encuentra ya tomado, avisa y se cierra sin llegar a abrir
// ninguna ventana.
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

        // El Mutex se queda dentro del "using" hasta que Main termina —
        // como Application.Start() bloquea mientras la app está abierta,
        // eso significa que se libera justo al cerrarla, no antes.
        System.Threading.Mutex mutexInstanciaUnica;
        bool esInstanciaNueva;

        try
        {
            mutexInstanciaUnica = new System.Threading.Mutex(true, NombreMutexInstanciaUnica, out esInstanciaNueva);
        }
        catch (UnauthorizedAccessException)
        {
            // Sin permiso para crear un Mutex "Global\" (pasa en algunos
            // entornos de Escritorio Remoto con políticas restrictivas) —
            // se usa uno de sesión en vez de tronar. Sigue evitando dos
            // instancias para el mismo usuario, aunque no para toda la
            // computadora si hay varias sesiones activas.
            mutexInstanciaUnica = new System.Threading.Mutex(true, NombreMutexInstanciaUnica.Replace(@"Global\", string.Empty), out esInstanciaNueva);
        }

        using var _ = mutexInstanciaUnica;

        if (!esInstanciaNueva)
        {
            MessageBox(IntPtr.Zero, "Alejandría ya está abierta en esta computadora.", "Alejandría", MB_ICONINFORMATION);
            return;
        }

        // Estos dos capturan errores que no pasan por el manejador de
        // App.xaml.cs (hilos aparte, tareas async sin esperar) — sin esto,
        // esos errores no quedarían registrados en ningún lado.
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
