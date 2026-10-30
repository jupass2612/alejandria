using Microsoft.UI.Xaml;
using Velopack;

namespace AlejandriaCliente;

// Mismo patrón que Alejandria: Main propio para correr
// VelopackApp.Build().Run() antes de que arranque la UI.
public static class Program
{
    [global::System.Runtime.InteropServices.DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

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
