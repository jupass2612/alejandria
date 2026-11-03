using System.Diagnostics;

namespace Alejandria.Services;

// Abre un archivo con su programa de Windows
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

    // Abre la carpeta en el Explorador, la crea si no existe
    public static void AbrirCarpeta(string ruta)
    {
        Directory.CreateDirectory(ruta);
        Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
    }
}
