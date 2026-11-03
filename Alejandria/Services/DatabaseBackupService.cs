namespace Alejandria.Services;

// Respaldo diario de la base en Respaldos, se queda con 30 días
public static class DatabaseBackupService
{
    private const int DiasARetener = 30;

    public static async Task RespaldarSiHaceFaltaAsync(string rutaBaseDatos)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rutaBaseDatos) || !File.Exists(rutaBaseDatos))
            {
                return;
            }

            var carpetaBase = Path.GetDirectoryName(rutaBaseDatos);
            if (string.IsNullOrWhiteSpace(carpetaBase))
            {
                return;
            }

            var carpetaRespaldos = Path.Combine(carpetaBase, "Respaldos");
            Directory.CreateDirectory(carpetaRespaldos);

            var nombreBase = Path.GetFileNameWithoutExtension(rutaBaseDatos);
            var extension = Path.GetExtension(rutaBaseDatos);
            var rutaRespaldoHoy = Path.Combine(carpetaRespaldos, $"{nombreBase}_{DateTime.Now:yyyy-MM-dd}{extension}");

            // Ya hay respaldo de hoy
            if (!File.Exists(rutaRespaldoHoy))
            {
                await Task.Run(() => File.Copy(rutaBaseDatos, rutaRespaldoHoy, overwrite: false));
                LogService.LogInfo($"Respaldo de base de datos creado: {rutaRespaldoHoy}");
            }

            LimpiarRespaldosViejos(carpetaRespaldos, nombreBase, extension);
        }
        catch (Exception ex)
        {
            // Si falla, solo queda en el log
            LogService.LogError("DatabaseBackupService.RespaldarSiHaceFaltaAsync", ex);
        }
    }

    private static void LimpiarRespaldosViejos(string carpetaRespaldos, string nombreBase, string extension)
    {
        var limite = DateTime.Now.AddDays(-DiasARetener);

        foreach (var archivo in Directory.GetFiles(carpetaRespaldos, $"{nombreBase}_*{extension}"))
        {
            if (File.GetCreationTime(archivo) < limite)
            {
                File.Delete(archivo);
            }
        }
    }
}
