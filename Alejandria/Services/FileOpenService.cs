using System.Diagnostics;

namespace Alejandria.Services;

// Abre cualquier archivo con la aplicación predeterminada de Windows.
public static class FileOpenService
{
    public static void Abrir(string rutaCompleta)
    {
        if (!File.Exists(rutaCompleta))
        {
            throw new FileNotFoundException("El archivo ya no está en esa ubicación (¿se movió o se borró?).", rutaCompleta);
        }

        Process.Start(new ProcessStartInfo(rutaCompleta) { UseShellExecute = true });
    }

    // Abre una carpeta en el Explorador de Windows — la crea primero si
    // no existe (ej. la carpeta de logs, antes de que se haya escrito el
    // primer error), para no fallar solo por eso.
    public static void AbrirCarpeta(string ruta)
    {
        Directory.CreateDirectory(ruta);
        Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
    }
}
