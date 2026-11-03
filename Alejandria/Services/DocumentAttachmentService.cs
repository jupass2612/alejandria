using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Alejandria.Services;

// PDFs adjuntos en la carpeta Documentos junto a la base
public static class DocumentAttachmentService
{
    private const string CarpetaDocumentos = "Documentos";

    // Copia el PDF y regresa el nombre con que quedó
    public static async Task<string> AdjuntarAsync(string databasePath, int modeloId, string archivoOrigen)
    {
        var carpeta = ObtenerCarpetaDocumentos(databasePath);
        Directory.CreateDirectory(carpeta);

        var nombreOriginal = SanitizarNombreArchivo(Path.GetFileName(archivoOrigen));
        var nombreGuardado = $"{modeloId}_{DateTime.Now:yyyyMMddHHmmss}_{nombreOriginal}";
        var destino = Path.Combine(carpeta, nombreGuardado);

        using (var origen = File.OpenRead(archivoOrigen))
        using (var salida = File.Create(destino))
        {
            await origen.CopyToAsync(salida);
        }

        return nombreGuardado;
    }

    // Abre el PDF con el visor de Windows
    public static void Abrir(string databasePath, string nombreArchivo)
    {
        var ruta = Path.Combine(ObtenerCarpetaDocumentos(databasePath), nombreArchivo);
        FileOpenService.Abrir(ruta);
    }

    // Ruta completa del PDF
    public static string ObtenerRutaCompleta(string databasePath, string nombreArchivo) =>
        Path.Combine(ObtenerCarpetaDocumentos(databasePath), nombreArchivo);

    private static string ObtenerCarpetaDocumentos(string databasePath)
    {
        var carpetaBase = Path.GetDirectoryName(databasePath)
            ?? throw new InvalidOperationException("No se pudo determinar la carpeta de la base de datos compartida.");
        return Path.Combine(carpetaBase, CarpetaDocumentos);
    }

    private static string SanitizarNombreArchivo(string nombre)
    {
        var invalidos = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        return Regex.Replace(nombre, $"[{invalidos}]", "_");
    }
}
