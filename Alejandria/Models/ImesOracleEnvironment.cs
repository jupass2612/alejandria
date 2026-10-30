namespace Alejandria.Models;

// Datos de conexión de un entorno de iMES (Productivo o Pruebas)
public class ImesOracleEnvironment
{
    public string Host { get; set; } = string.Empty;
    public int Puerto { get; set; } = 1521;

    // Nombre del servicio o SID de Oracle
    public string ServicioOSid { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(ServicioOSid);

    // Cadena de conexión para Oracle.ManagedDataAccess.
    public string ConnectionString =>
        $"User Id={Usuario};Password={Password};Data Source={Host}:{Puerto}/{ServicioOSid};";
}
