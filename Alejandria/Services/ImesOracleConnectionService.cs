using Alejandria.Models;
using Oracle.ManagedDataAccess.Client;

namespace Alejandria.Services;

// Primer paso hacia que Alejandría configure directamente en iMES (en
// vez de solo llevar el seguimiento de que alguien más ya lo hizo a
// mano): conexión a la base Oracle de iMES, parametrizable entre los 3
// entornos (Productivo/Pruebas/Docker) definidos en appsettings.json.
//
// Por ahora SOLO prueba que la conexión funciona — la lectura/escritura
// real de MODELS_MASTER, MODELS_PARENTS y MODELS_SERIALNUMBERSPECS es
// el siguiente paso, una vez que esto esté probado contra el entorno
// Docker (ver docker/README.md).
public class ImesOracleConnectionService
{
    private readonly ImesOracleEnvironment _entorno;

    public ImesOracleConnectionService(ImesOracleEnvironment entorno)
    {
        _entorno = entorno;
    }

    // Abre la conexión y corre una consulta mínima — para confirmar que
    // el entorno elegido (Docker/Pruebas/Productivo) está bien
    // configurado y accesible, sin tocar ninguna tabla todavía.
    public async Task<(bool Exito, string? Error)> ProbarConexionAsync()
    {
        if (!_entorno.EstaConfigurado)
        {
            return (false, "Este entorno no tiene Host/ServicioOSid configurados en appsettings.json.");
        }

        try
        {
            await using var connection = new OracleConnection(_entorno.ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1 FROM DUAL";
            await command.ExecuteScalarAsync();

            return (true, null);
        }
        catch (Exception ex)
        {
            LogService.LogError("ImesOracleConnectionService.ProbarConexionAsync", ex);
            return (false, LogService.Redactar(ex.Message));
        }
    }

    // Cuenta filas en las 3 tablas — otra forma rápida de confirmar que
    // el esquema del entorno elegido coincide con lo esperado (ej. que
    // el entorno Docker sí corrió los scripts de init).
    public async Task<(bool Exito, string? Error, int Parents, int Master, int SerialSpecs)> ProbarEsquemaAsync()
    {
        try
        {
            await using var connection = new OracleConnection(_entorno.ConnectionString);
            await connection.OpenAsync();

            async Task<int> ContarAsync(string tabla)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM IMES.{tabla}";
                var resultado = await command.ExecuteScalarAsync();
                return Convert.ToInt32(resultado);
            }

            var parents = await ContarAsync("MODELS_PARENTS");
            var master = await ContarAsync("MODELS_MASTER");
            var serialSpecs = await ContarAsync("MODELS_SERIALNUMBERSPECS");

            return (true, null, parents, master, serialSpecs);
        }
        catch (Exception ex)
        {
            LogService.LogError("ImesOracleConnectionService.ProbarEsquemaAsync", ex);
            return (false, LogService.Redactar(ex.Message), 0, 0, 0);
        }
    }
}
