namespace Alejandria.Services;

// Colores por estatus en un solo lugar; los reportes siempre van en claro
public static class StatusColorHelper
{
    public const string AccentHex = "#2563EB";

    public static string GetHex(string status) => status switch
    {
        ConfigStatusHelper.Terminado => "#22C55E", // Success
        ConfigStatusHelper.EnProceso => "#F59E0B", // Warning
        ConfigStatusHelper.Bloqueado => "#EF4444", // Danger
        ConfigStatusHelper.YaExistente => "#0EA5E9", // Info
        _ when Array.IndexOf(ConfigStatusHelper.Responsables, status) >= 0 => "#22C55E", // Persona = terminado
        _ => "#94A3B8" // Pendiente o desconocido
    };

    public static (byte R, byte G, byte B) GetRgb(string status)
    {
        var hex = GetHex(status).TrimStart('#');
        var r = System.Convert.ToByte(hex.Substring(0, 2), 16);
        var g = System.Convert.ToByte(hex.Substring(2, 2), 16);
        var b = System.Convert.ToByte(hex.Substring(4, 2), 16);
        return (r, g, b);
    }
}
