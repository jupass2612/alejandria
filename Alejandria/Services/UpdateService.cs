using Velopack;
using Velopack.Sources;

namespace Alejandria.Services;

public class ResultadoBusquedaActualizacion
{
    public bool HayActualizacion { get; init; }
    public string? VersionDisponible { get; init; }
    public UpdateInfo? Info { get; init; }
    public string? Error { get; init; }
}

// Actualizaciones con Velopack desde la carpeta de red, sin servidor ni internet
public static class UpdateService
{
    private static UpdateManager CrearManager(string carpetaActualizaciones) =>
        new(new SimpleFileSource(new DirectoryInfo(carpetaActualizaciones)));

    // Sin instalar con Velopack (corriendo desde el IDE) no hay versión
    public static string VersionActualTexto(string carpetaActualizaciones)
    {
        try
        {
            var mgr = CrearManager(carpetaActualizaciones);
            return mgr.IsInstalled ? mgr.CurrentVersion?.ToString() ?? "desconocida" : "modo desarrollo (sin instalar)";
        }
        catch (Exception)
        {
            return "desconocida";
        }
    }

    public static async Task<ResultadoBusquedaActualizacion> BuscarActualizacionAsync(string carpetaActualizaciones)
    {
        if (string.IsNullOrWhiteSpace(carpetaActualizaciones))
        {
            return new ResultadoBusquedaActualizacion
            {
                Error = "No hay una carpeta de actualizaciones configurada (Updates:Folder en appsettings.json)."
            };
        }

        try
        {
            var mgr = CrearManager(carpetaActualizaciones);

            if (!mgr.IsInstalled)
            {
                return new ResultadoBusquedaActualizacion
                {
                    Error = "Esta copia no está instalada con Velopack (¿la estás corriendo desde Visual Studio?) — las actualizaciones solo aplican a una instalación real."
                };
            }

            var info = await mgr.CheckForUpdatesAsync();
            if (info is null)
            {
                return new ResultadoBusquedaActualizacion
                {
                    HayActualizacion = false,
                    VersionDisponible = mgr.CurrentVersion?.ToString()
                };
            }

            return new ResultadoBusquedaActualizacion
            {
                HayActualizacion = true,
                VersionDisponible = info.TargetFullRelease.Version.ToString(),
                Info = info
            };
        }
        catch (Exception ex)
        {
            LogService.LogError("UpdateService.BuscarActualizacionAsync", ex);
            return new ResultadoBusquedaActualizacion { Error = "No se pudo revisar si hay actualizaciones — revisa tu conexión de red o VPN de la empresa. Más detalles en el registro de errores (Ajustes)." };
        }
    }

    public static async Task<(bool Exito, string? Error)> DescargarActualizacionAsync(string carpetaActualizaciones, UpdateInfo info)
    {
        try
        {
            var mgr = CrearManager(carpetaActualizaciones);
            await mgr.DownloadUpdatesAsync(info);
            return (true, null);
        }
        catch (Exception ex)
        {
            LogService.LogError("UpdateService.DescargarActualizacionAsync", ex);
            return (false, "No se pudo descargar la actualización — revisa tu conexión de red o VPN de la empresa. Más detalles en el registro de errores (Ajustes).");
        }
    }

    // Cierra, aplica la actualización y abre otra vez
    public static void InstalarYReiniciar(string carpetaActualizaciones, UpdateInfo info)
    {
        var mgr = CrearManager(carpetaActualizaciones);
        mgr.ApplyUpdatesAndRestart(info);
    }
}
