using System.Globalization;
using System.Threading;
using Alejandria.Models;
using Microsoft.Data.Sqlite;

namespace Alejandria.Services;

// Acceso a la base SQLite de "Altas de Modelos". El .db vive en una ruta
// de red compartida para que todas las computadoras lean/escriban lo mismo.
// Se usa el journal normal (no WAL, SQLite lo desaconseja en red) con un
// busy_timeout generoso para los "database is locked" ocasionales.
public class ModelDatabaseService
{
    private const string TableName = "AltasModelos";
    private const string DetalleTableName = "DetalleConfiguracion";
    private readonly string _connectionString;

    // Optimización: InitializeAsync se llama cada vez que se abre una pantalla
    // (Altas, Modelos, Estadísticas...), pero el esquema solo necesita
    // verificarse/crearse una vez por ejecución de la app. Este flag evita
    // pagar ese round-trip de red repetidamente. Es estático a propósito:
    // cada pantalla crea su propia instancia de ModelDatabaseService, pero
    // todas apuntan al mismo archivo, así que el estado se comparte.
    private static volatile bool _schemaVerified;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    public ModelDatabaseService(string databasePath)
    {
        // La ruta de red ya existe siempre, pero la carpeta de pruebas
        // local (entorno Docker) puede no existir todavía la primera vez.
        var carpeta = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        _connectionString = $"Data Source={databasePath}";
    }

    public async Task InitializeAsync()
    {
        if (_schemaVerified)
        {
            return;
        }

        await SchemaLock.WaitAsync();
        try
        {
            if (_schemaVerified)
            {
                return;
            }

            using var connection = await OpenConnectionAsync();

            using var command = connection.CreateCommand();
            command.CommandText = $"""
                CREATE TABLE IF NOT EXISTS {TableName} (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    NumeroModelo TEXT NOT NULL,
                    NombreModelo TEXT NOT NULL,
                    NumeroParteSMT TEXT,
                    NumeroParteBE TEXT,
                    NumeroParteTLA TEXT,
                    QuienRecibeDocumento TEXT,
                    FechaSolicitud TEXT NOT NULL,
                    QuienMandoDocumento TEXT
                );
                """;
            await command.ExecuteNonQueryAsync();

            // Migración: agrega columnas que no existían en versiones anteriores
            // de la app, sin perder los datos ya guardados. "Area" se deja de
            // usar pero no se borra (por si se necesita consultar qué tenía
            // antes); los 3 números de parte por área son las columnas nuevas.
            await EnsureColumnExistsAsync(connection, TableName, "Area", "TEXT");
            await EnsureColumnExistsAsync(connection, TableName, "NumeroParteSMT", "TEXT");
            await EnsureColumnExistsAsync(connection, TableName, "NumeroParteBE", "TEXT");
            await EnsureColumnExistsAsync(connection, TableName, "NumeroParteTLA", "TEXT");
            await EnsureColumnExistsAsync(connection, TableName, "DocumentoAdjunto", "TEXT");

            // Campo nuevo: el modelo padre de iMES elegido/creado desde
            // "Configurar en iMES" — es del modelo completo, no de un área,
            // así se recuerda una sola vez sin importar desde qué área se hizo.
            await EnsureColumnExistsAsync(connection, TableName, "OracleParentId", "INTEGER");
            await EnsureColumnExistsAsync(connection, TableName, "OracleParentNombre", "TEXT");

            await EnsureDetalleTableAsync(connection);

            // Campo nuevo: "MODELS_SERIALNUMBERSPECS Packing" en iMES y
            // Unity — se agrega así (en vez de solo en el CREATE TABLE)
            // para que también llegue a bases de datos que ya existían
            // antes de este campo, sin perder nada de lo ya capturado.
            await EnsureColumnExistsAsync(connection, DetalleTableName, "ImesModelsSerialNumberSpecsPacking", "TEXT");
            await EnsureColumnExistsAsync(connection, DetalleTableName, "UnityModelsSerialNumberSpecsPacking", "TEXT");

            // Campo nuevo: "Estado del Documento" (Correcto / Pendiente a
            // correcciones) — reemplaza al viejo valor "Bloqueado por
            // Documento Malo" que antes se elegía en los campos de BD.
            await EnsureColumnExistsAsync(connection, DetalleTableName, "EstadoDocumento", "TEXT");

            _schemaVerified = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    // Crea la tabla DetalleConfiguracion (clave ModeloId+Area) o migra la
    // versión vieja (una fila por modelo, sin columna Area) si la encuentra.
    private async Task EnsureDetalleTableAsync(SqliteConnection connection)
    {
        using var checkTableCommand = connection.CreateCommand();
        checkTableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name;";
        checkTableCommand.Parameters.AddWithValue("$name", DetalleTableName);
        var tableExists = await checkTableCommand.ExecuteScalarAsync() is not null;

        if (!tableExists)
        {
            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = $"""
                CREATE TABLE {DetalleTableName} (
                    ModeloId INTEGER NOT NULL,
                    Area TEXT NOT NULL,
                    ImesModelsParents TEXT,
                    ImesModelsMaster TEXT,
                    ImesModelsSerialNumberSpecs TEXT,
                    ImesModelsSerialNumberSpecsPacking TEXT,
                    UnityModelsParents TEXT,
                    UnityModelsMaster TEXT,
                    UnityModelsSerialNumberSpecs TEXT,
                    UnityModelsSerialNumberSpecsPacking TEXT,
                    WorkOrder TEXT,
                    Flujo TEXT,
                    Ruta TEXT,
                    Linea TEXT,
                    PartLineBomMaintenance TEXT,
                    EstadoDocumento TEXT,
                    PRIMARY KEY (ModeloId, Area),
                    FOREIGN KEY (ModeloId) REFERENCES {TableName}(Id)
                );
                """;
            await createCommand.ExecuteNonQueryAsync();
            return;
        }

        var tieneArea = await ColumnExistsAsync(connection, DetalleTableName, "Area");
        if (tieneArea)
        {
            return; // Ya está en el esquema nuevo.
        }

        // Esquema viejo (un registro por modelo, sin Area): migrar a la
        // tabla nueva con clave compuesta, atribuyendo cada configuración
        // existente al área principal del modelo (SMT > BE > TLA) para no
        // perder lo que ya se había capturado.
        using (var createNewCommand = connection.CreateCommand())
        {
            createNewCommand.CommandText = $"""
                CREATE TABLE {DetalleTableName}_new (
                    ModeloId INTEGER NOT NULL,
                    Area TEXT NOT NULL,
                    ImesModelsParents TEXT,
                    ImesModelsMaster TEXT,
                    ImesModelsSerialNumberSpecs TEXT,
                    ImesModelsSerialNumberSpecsPacking TEXT,
                    UnityModelsParents TEXT,
                    UnityModelsMaster TEXT,
                    UnityModelsSerialNumberSpecs TEXT,
                    UnityModelsSerialNumberSpecsPacking TEXT,
                    WorkOrder TEXT,
                    Flujo TEXT,
                    Ruta TEXT,
                    Linea TEXT,
                    PartLineBomMaintenance TEXT,
                    EstadoDocumento TEXT,
                    PRIMARY KEY (ModeloId, Area),
                    FOREIGN KEY (ModeloId) REFERENCES {TableName}(Id)
                );
                """;
            await createNewCommand.ExecuteNonQueryAsync();
        }

        using (var copyCommand = connection.CreateCommand())
        {
            copyCommand.CommandText = $"""
                INSERT INTO {DetalleTableName}_new
                    (ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs,
                     UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs,
                     WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance)
                SELECT
                    d.ModeloId,
                    CASE
                        WHEN TRIM(COALESCE(a.NumeroParteSMT, '')) <> '' THEN 'SMT'
                        WHEN TRIM(COALESCE(a.NumeroParteBE, '')) <> '' THEN 'BE'
                        WHEN TRIM(COALESCE(a.NumeroParteTLA, '')) <> '' THEN 'TLA'
                        ELSE 'SMT'
                    END,
                    d.ImesModelsParents, d.ImesModelsMaster, d.ImesModelsSerialNumberSpecs,
                    d.UnityModelsParents, d.UnityModelsMaster, d.UnityModelsSerialNumberSpecs,
                    d.WorkOrder, d.Flujo, d.Ruta, d.Linea, d.PartLineBomMaintenance
                FROM {DetalleTableName} d
                JOIN {TableName} a ON a.Id = d.ModeloId;
                """;
            await copyCommand.ExecuteNonQueryAsync();
        }

        using (var dropCommand = connection.CreateCommand())
        {
            dropCommand.CommandText = $"DROP TABLE {DetalleTableName};";
            await dropCommand.ExecuteNonQueryAsync();
        }

        using (var renameCommand = connection.CreateCommand())
        {
            renameCommand.CommandText = $"ALTER TABLE {DetalleTableName}_new RENAME TO {DetalleTableName};";
            await renameCommand.ExecuteNonQueryAsync();
        }
    }

    // Agrega una columna si todavía no existe (migración segura, no destructiva).
    private static async Task EnsureColumnExistsAsync(SqliteConnection connection, string tableName, string columnName, string columnType)
    {
        if (await ColumnExistsAsync(connection, tableName, columnName))
        {
            return;
        }

        using var addColumnCommand = connection.CreateCommand();
        addColumnCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType};";
        await addColumnCommand.ExecuteNonQueryAsync();
    }

    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string tableName, string columnName)
    {
        using var checkColumnCommand = connection.CreateCommand();
        checkColumnCommand.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = await checkColumnCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<List<ModelRecord>> GetAllAsync()
    {
        var results = new List<ModelRecord>();

        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT Id, NumeroModelo, NombreModelo, NumeroParteSMT, NumeroParteBE, NumeroParteTLA, QuienRecibeDocumento, FechaSolicitud, QuienMandoDocumento, DocumentoAdjunto, OracleParentId, OracleParentNombre
            FROM {TableName}
            ORDER BY Id DESC;
            """;

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadModelRecord(reader));
        }

        return results;
    }

    // Trae los modelos y toda su configuración en una sola conexión (dos
    // consultas), en vez de abrir la conexión dos veces por separado.
    public async Task<(List<ModelRecord> Modelos, Dictionary<int, List<DetalleConfiguracion>> Detalles)> GetModelosConDetallesAsync()
    {
        using var connection = await OpenConnectionAsync();

        var modelos = new List<ModelRecord>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT Id, NumeroModelo, NombreModelo, NumeroParteSMT, NumeroParteBE, NumeroParteTLA, QuienRecibeDocumento, FechaSolicitud, QuienMandoDocumento, DocumentoAdjunto, OracleParentId, OracleParentNombre
                FROM {TableName}
                ORDER BY Id DESC;
                """;
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                modelos.Add(ReadModelRecord(reader));
            }
        }

        var detalles = new Dictionary<int, List<DetalleConfiguracion>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs, ImesModelsSerialNumberSpecsPacking,
                       UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs, UnityModelsSerialNumberSpecsPacking,
                       WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento
                FROM {DetalleTableName};
                """;
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var detalle = ReadDetalle(reader);
                if (!detalles.TryGetValue(detalle.ModeloId, out var lista))
                {
                    lista = new List<DetalleConfiguracion>();
                    detalles[detalle.ModeloId] = lista;
                }

                lista.Add(detalle);
            }
        }

        return (modelos, detalles);
    }

    public async Task<int> InsertAsync(ModelRecord record)
    {
        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {TableName} (NumeroModelo, NombreModelo, NumeroParteSMT, NumeroParteBE, NumeroParteTLA, QuienRecibeDocumento, FechaSolicitud, QuienMandoDocumento)
            VALUES ($numeroModelo, $nombreModelo, $parteSmt, $parteBe, $parteTla, $quienRecibe, $fechaSolicitud, $quienManda);
            SELECT last_insert_rowid();
            """;

        command.Parameters.AddWithValue("$numeroModelo", record.NumeroModelo);
        command.Parameters.AddWithValue("$nombreModelo", record.NombreModelo);
        command.Parameters.AddWithValue("$parteSmt", ToDb(record.NumeroParteSmt));
        command.Parameters.AddWithValue("$parteBe", ToDb(record.NumeroParteBe));
        command.Parameters.AddWithValue("$parteTla", ToDb(record.NumeroParteTla));
        command.Parameters.AddWithValue("$quienRecibe", string.IsNullOrEmpty(record.QuienRecibeDocumento) ? DBNull.Value : record.QuienRecibeDocumento);
        command.Parameters.AddWithValue("$fechaSolicitud", record.FechaSolicitud.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$quienManda", string.IsNullOrEmpty(record.QuienMandoDocumento) ? DBNull.Value : record.QuienMandoDocumento);

        var newId = await command.ExecuteScalarAsync();
        return Convert.ToInt32(newId, CultureInfo.InvariantCulture);
    }

    public async Task UpdateAsync(ModelRecord record)
    {
        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            UPDATE {TableName}
            SET NumeroModelo = $numeroModelo,
                NombreModelo = $nombreModelo,
                NumeroParteSMT = $parteSmt,
                NumeroParteBE = $parteBe,
                NumeroParteTLA = $parteTla,
                QuienRecibeDocumento = $quienRecibe,
                FechaSolicitud = $fechaSolicitud,
                QuienMandoDocumento = $quienManda
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", record.Id);
        command.Parameters.AddWithValue("$numeroModelo", record.NumeroModelo);
        command.Parameters.AddWithValue("$nombreModelo", record.NombreModelo);
        command.Parameters.AddWithValue("$parteSmt", ToDb(record.NumeroParteSmt));
        command.Parameters.AddWithValue("$parteBe", ToDb(record.NumeroParteBe));
        command.Parameters.AddWithValue("$parteTla", ToDb(record.NumeroParteTla));
        command.Parameters.AddWithValue("$quienRecibe", string.IsNullOrEmpty(record.QuienRecibeDocumento) ? DBNull.Value : record.QuienRecibeDocumento);
        command.Parameters.AddWithValue("$fechaSolicitud", record.FechaSolicitud.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$quienManda", string.IsNullOrEmpty(record.QuienMandoDocumento) ? DBNull.Value : record.QuienMandoDocumento);

        await command.ExecuteNonQueryAsync();
    }

    // Todas las configuraciones de todos los modelos, agrupadas por ModeloId.
    public async Task<Dictionary<int, List<DetalleConfiguracion>>> GetAllDetallesAsync()
    {
        var results = new Dictionary<int, List<DetalleConfiguracion>>();

        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs, ImesModelsSerialNumberSpecsPacking,
                   UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs, UnityModelsSerialNumberSpecsPacking,
                   WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento
            FROM {DetalleTableName};
            """;

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var detalle = ReadDetalle(reader);
            if (!results.TryGetValue(detalle.ModeloId, out var lista))
            {
                lista = new List<DetalleConfiguracion>();
                results[detalle.ModeloId] = lista;
            }

            lista.Add(detalle);
        }

        return results;
    }

    // Config de un área de un modelo. Null si esa área no tiene nada capturado.
    public async Task<DetalleConfiguracion?> GetDetalleAsync(int modeloId, string area)
    {
        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs, ImesModelsSerialNumberSpecsPacking,
                   UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs, UnityModelsSerialNumberSpecsPacking,
                   WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento
            FROM {DetalleTableName}
            WHERE ModeloId = $modeloId AND Area = $area;
            """;
        command.Parameters.AddWithValue("$modeloId", modeloId);
        command.Parameters.AddWithValue("$area", area);

        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadDetalle(reader) : null;
    }

    // Todas las configuraciones (todas las áreas) de un modelo.
    public async Task<List<DetalleConfiguracion>> GetDetallesDeModeloAsync(int modeloId)
    {
        var results = new List<DetalleConfiguracion>();

        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs, ImesModelsSerialNumberSpecsPacking,
                   UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs, UnityModelsSerialNumberSpecsPacking,
                   WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento
            FROM {DetalleTableName}
            WHERE ModeloId = $modeloId;
            """;
        command.Parameters.AddWithValue("$modeloId", modeloId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadDetalle(reader));
        }

        return results;
    }

    // Inserta o actualiza la config de un área — la clave es (ModeloId, Area).
    public async Task SaveDetalleAsync(DetalleConfiguracion detalle)
    {
        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {DetalleTableName}
                (ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs, ImesModelsSerialNumberSpecsPacking,
                 UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs, UnityModelsSerialNumberSpecsPacking,
                 WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento)
            VALUES
                ($modeloId, $area, $imesParents, $imesMaster, $imesSerial, $imesSerialPacking,
                 $unityParents, $unityMaster, $unitySerial, $unitySerialPacking,
                 $workOrder, $flujo, $ruta, $linea, $partLine, $estadoDocumento)
            ON CONFLICT(ModeloId, Area) DO UPDATE SET
                ImesModelsParents = excluded.ImesModelsParents,
                ImesModelsMaster = excluded.ImesModelsMaster,
                ImesModelsSerialNumberSpecs = excluded.ImesModelsSerialNumberSpecs,
                ImesModelsSerialNumberSpecsPacking = excluded.ImesModelsSerialNumberSpecsPacking,
                UnityModelsParents = excluded.UnityModelsParents,
                UnityModelsMaster = excluded.UnityModelsMaster,
                UnityModelsSerialNumberSpecs = excluded.UnityModelsSerialNumberSpecs,
                UnityModelsSerialNumberSpecsPacking = excluded.UnityModelsSerialNumberSpecsPacking,
                WorkOrder = excluded.WorkOrder,
                Flujo = excluded.Flujo,
                Ruta = excluded.Ruta,
                Linea = excluded.Linea,
                PartLineBomMaintenance = excluded.PartLineBomMaintenance,
                EstadoDocumento = excluded.EstadoDocumento;
            """;

        command.Parameters.AddWithValue("$modeloId", detalle.ModeloId);
        command.Parameters.AddWithValue("$area", detalle.Area);
        command.Parameters.AddWithValue("$imesParents", ToDb(detalle.ImesModelsParents));
        command.Parameters.AddWithValue("$imesMaster", ToDb(detalle.ImesModelsMaster));
        command.Parameters.AddWithValue("$imesSerial", ToDb(detalle.ImesModelsSerialNumberSpecs));
        command.Parameters.AddWithValue("$imesSerialPacking", ToDb(detalle.ImesModelsSerialNumberSpecsPacking));
        command.Parameters.AddWithValue("$unityParents", ToDb(detalle.UnityModelsParents));
        command.Parameters.AddWithValue("$unityMaster", ToDb(detalle.UnityModelsMaster));
        command.Parameters.AddWithValue("$unitySerial", ToDb(detalle.UnityModelsSerialNumberSpecs));
        command.Parameters.AddWithValue("$unitySerialPacking", ToDb(detalle.UnityModelsSerialNumberSpecsPacking));
        command.Parameters.AddWithValue("$workOrder", ToDb(detalle.WorkOrder));
        command.Parameters.AddWithValue("$flujo", ToDb(detalle.Flujo));
        command.Parameters.AddWithValue("$ruta", ToDb(detalle.Ruta));
        command.Parameters.AddWithValue("$linea", ToDb(detalle.Linea));
        command.Parameters.AddWithValue("$partLine", ToDb(detalle.PartLineBomMaintenance));
        command.Parameters.AddWithValue("$estadoDocumento", ToDb(detalle.EstadoDocumento));

        await command.ExecuteNonQueryAsync();
    }

    // Actualiza solo el nombre del PDF adjunto.
    public async Task UpdateDocumentoAdjuntoAsync(int modeloId, string nombreArchivo)
    {
        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {TableName} SET DocumentoAdjunto = $documento WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", modeloId);
        command.Parameters.AddWithValue("$documento", ToDb(nombreArchivo));

        await command.ExecuteNonQueryAsync();
    }

    // Guarda a qué modelo padre de iMES quedó ligado este modelo — es del
    // modelo completo (no de un área), se recuerda aquí para no volver a
    // buscarlo desde otra área.
    public async Task UpdateOracleParentAsync(int modeloId, int parentId, string parentNombre)
    {
        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {TableName} SET OracleParentId = $parentId, OracleParentNombre = $parentNombre WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", modeloId);
        command.Parameters.AddWithValue("$parentId", parentId);
        command.Parameters.AddWithValue("$parentNombre", ToDb(parentNombre));

        await command.ExecuteNonQueryAsync();
    }

    private static object ToDb(string? value) => string.IsNullOrEmpty(value) ? DBNull.Value : value;

    private static string ReadString(SqliteDataReader reader, int index) => reader.IsDBNull(index) ? string.Empty : reader.GetString(index);

    private static ModelRecord ReadModelRecord(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        NumeroModelo = reader.GetString(1),
        NombreModelo = reader.GetString(2),
        NumeroParteSmt = ReadString(reader, 3),
        NumeroParteBe = ReadString(reader, 4),
        NumeroParteTla = ReadString(reader, 5),
        QuienRecibeDocumento = ReadString(reader, 6),
        FechaSolicitud = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
        QuienMandoDocumento = ReadString(reader, 8),
        DocumentoAdjunto = ReadString(reader, 9),
        OracleParentId = reader.IsDBNull(10) ? null : reader.GetInt32(10),
        OracleParentNombre = ReadString(reader, 11)
    };

    private static DetalleConfiguracion ReadDetalle(SqliteDataReader reader) => new()
    {
        ModeloId = reader.GetInt32(0),
        Area = ReadString(reader, 1),
        ImesModelsParents = ReadString(reader, 2),
        ImesModelsMaster = ReadString(reader, 3),
        ImesModelsSerialNumberSpecs = ReadString(reader, 4),
        ImesModelsSerialNumberSpecsPacking = ReadString(reader, 5),
        UnityModelsParents = ReadString(reader, 6),
        UnityModelsMaster = ReadString(reader, 7),
        UnityModelsSerialNumberSpecs = ReadString(reader, 8),
        UnityModelsSerialNumberSpecsPacking = ReadString(reader, 9),
        WorkOrder = ReadString(reader, 10),
        Flujo = ReadString(reader, 11),
        Ruta = ReadString(reader, 12),
        Linea = ReadString(reader, 13),
        PartLineBomMaintenance = ReadString(reader, 14),
        EstadoDocumento = string.IsNullOrEmpty(ReadString(reader, 15)) ? "Correcto" : ReadString(reader, 15)
    };

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);

        // Si la ruta de red no está disponible (VPN apagada, fuera de la red
        // de la empresa, el servidor caído...), abrir la conexión puede
        // tardar mucho en fallar por sí sola (el sistema operativo reintenta
        // la resolución de la ruta UNC). Con este límite, la app avisa en
        // unos segundos en vez de quedarse "trabada" sin explicación.
        var openTask = connection.OpenAsync();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(8));
        var finished = await Task.WhenAny(openTask, timeoutTask);

        if (finished == timeoutTask)
        {
            connection.Dispose();
            throw new TimeoutException(
                "No se pudo conectar a la base de datos compartida en 8 segundos. " +
                "Verifica tu conexión de red (VPN o red de la empresa) y que la ruta configurada exista.");
        }

        await openTask; // Propaga cualquier excepción real de apertura (permisos, ruta inválida, etc.).

        using var pragma = connection.CreateCommand();
        // Espera hasta 5s si otra computadora tiene el archivo bloqueado en ese instante,
        // en vez de fallar de inmediato con "database is locked".
        pragma.CommandText = "PRAGMA busy_timeout = 5000;";
        await pragma.ExecuteNonQueryAsync();

        return connection;
    }
}
