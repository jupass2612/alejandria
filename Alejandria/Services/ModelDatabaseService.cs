using System.Globalization;
using System.Threading;
using Alejandria.Models;
using Microsoft.Data.Sqlite;

namespace Alejandria.Services;

// SQLite de Altas de Modelos en la red compartida.
// Sin WAL (no va en red) y con busy_timeout para los database is locked
public class ModelDatabaseService
{
    private const string TableName = "AltasModelos";
    private const string DetalleTableName = "DetalleConfiguracion";
    private readonly string _connectionString;

    // El esquema se revisa una vez por ejecución; estático porque cada pantalla crea su instancia
    private static volatile bool _schemaVerified;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    public ModelDatabaseService(string databasePath)
    {
        // Por si la carpeta no existe
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

            // Migración: columnas nuevas sin perder datos (Area ya no se usa pero no se borra)
            await EnsureColumnExistsAsync(connection, TableName, "Area", "TEXT");
            await EnsureColumnExistsAsync(connection, TableName, "NumeroParteSMT", "TEXT");
            await EnsureColumnExistsAsync(connection, TableName, "NumeroParteBE", "TEXT");
            await EnsureColumnExistsAsync(connection, TableName, "NumeroParteTLA", "TEXT");
            await EnsureColumnExistsAsync(connection, TableName, "DocumentoAdjunto", "TEXT");

            await EnsureDetalleTableAsync(connection);

            // Packing de MODELS_SERIALNUMBERSPECS, para bases que ya existían
            await EnsureColumnExistsAsync(connection, DetalleTableName, "ImesModelsSerialNumberSpecsPacking", "TEXT");
            await EnsureColumnExistsAsync(connection, DetalleTableName, "UnityModelsSerialNumberSpecsPacking", "TEXT");

            // Estado del documento, reemplaza al viejo Bloqueado por Documento Malo
            await EnsureColumnExistsAsync(connection, DetalleTableName, "EstadoDocumento", "TEXT");

            // MODELID de MODELS_MASTER (Paso 2 → Paso 3)
            await EnsureColumnExistsAsync(connection, DetalleTableName, "ImesModelId", "TEXT");

            // SMT-BE: Work Order y Línea de BE (la Ruta de SMT va en Ruta y la de BE en Flujo)
            await EnsureColumnExistsAsync(connection, DetalleTableName, "WorkOrderBe", "TEXT");
            await EnsureColumnExistsAsync(connection, DetalleTableName, "LineaBe", "TEXT");

            _schemaVerified = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    // Crea DetalleConfiguracion (ModeloId+Area) o migra la versión vieja
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
            return; // Ya está en el esquema nuevo
        }

        // Esquema viejo: se migra pasando la config al área principal
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

    // Agrega la columna si no existe
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
            SELECT Id, NumeroModelo, NombreModelo, NumeroParteSMT, NumeroParteBE, NumeroParteTLA, QuienRecibeDocumento, FechaSolicitud, QuienMandoDocumento, DocumentoAdjunto
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

    // Modelos y su config en una sola conexión
    public async Task<(List<ModelRecord> Modelos, Dictionary<int, List<DetalleConfiguracion>> Detalles)> GetModelosConDetallesAsync()
    {
        using var connection = await OpenConnectionAsync();

        var modelos = new List<ModelRecord>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT Id, NumeroModelo, NombreModelo, NumeroParteSMT, NumeroParteBE, NumeroParteTLA, QuienRecibeDocumento, FechaSolicitud, QuienMandoDocumento, DocumentoAdjunto
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
                       WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento, ImesModelId, WorkOrderBe, LineaBe
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

    // Config de todos los modelos por ModeloId
    public async Task<Dictionary<int, List<DetalleConfiguracion>>> GetAllDetallesAsync()
    {
        var results = new Dictionary<int, List<DetalleConfiguracion>>();

        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs, ImesModelsSerialNumberSpecsPacking,
                   UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs, UnityModelsSerialNumberSpecsPacking,
                   WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento, ImesModelId, WorkOrderBe, LineaBe
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

    // Config de un área, null si no hay
    public async Task<DetalleConfiguracion?> GetDetalleAsync(int modeloId, string area)
    {
        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs, ImesModelsSerialNumberSpecsPacking,
                   UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs, UnityModelsSerialNumberSpecsPacking,
                   WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento, ImesModelId, WorkOrderBe, LineaBe
            FROM {DetalleTableName}
            WHERE ModeloId = $modeloId AND Area = $area;
            """;
        command.Parameters.AddWithValue("$modeloId", modeloId);
        command.Parameters.AddWithValue("$area", area);

        using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadDetalle(reader) : null;
    }

    // Config de todas las áreas del modelo
    public async Task<List<DetalleConfiguracion>> GetDetallesDeModeloAsync(int modeloId)
    {
        var results = new List<DetalleConfiguracion>();

        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs, ImesModelsSerialNumberSpecsPacking,
                   UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs, UnityModelsSerialNumberSpecsPacking,
                   WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento, ImesModelId, WorkOrderBe, LineaBe
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

    // Inserta o actualiza por (ModeloId, Area)
    public async Task SaveDetalleAsync(DetalleConfiguracion detalle)
    {
        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO {DetalleTableName}
                (ModeloId, Area, ImesModelsParents, ImesModelsMaster, ImesModelsSerialNumberSpecs, ImesModelsSerialNumberSpecsPacking,
                 UnityModelsParents, UnityModelsMaster, UnityModelsSerialNumberSpecs, UnityModelsSerialNumberSpecsPacking,
                 WorkOrder, Flujo, Ruta, Linea, PartLineBomMaintenance, EstadoDocumento, ImesModelId, WorkOrderBe, LineaBe)
            VALUES
                ($modeloId, $area, $imesParents, $imesMaster, $imesSerial, $imesSerialPacking,
                 $unityParents, $unityMaster, $unitySerial, $unitySerialPacking,
                 $workOrder, $flujo, $ruta, $linea, $partLine, $estadoDocumento, $imesModelId, $workOrderBe, $lineaBe)
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
                EstadoDocumento = excluded.EstadoDocumento,
                ImesModelId = excluded.ImesModelId,
                WorkOrderBe = excluded.WorkOrderBe,
                LineaBe = excluded.LineaBe;
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
        command.Parameters.AddWithValue("$imesModelId", ToDb(detalle.ImesModelId));
        command.Parameters.AddWithValue("$workOrderBe", ToDb(detalle.WorkOrderBe));
        command.Parameters.AddWithValue("$lineaBe", ToDb(detalle.LineaBe));

        await command.ExecuteNonQueryAsync();
    }

    // Solo el nombre del PDF
    public async Task UpdateDocumentoAdjuntoAsync(int modeloId, string nombreArchivo)
    {
        using var connection = await OpenConnectionAsync();
        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE {TableName} SET DocumentoAdjunto = $documento WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", modeloId);
        command.Parameters.AddWithValue("$documento", ToDb(nombreArchivo));

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
        QuienRecibeDocumento = ConfigStatusHelper.NormalizarResponsable(ReadString(reader, 6)),
        FechaSolicitud = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture),
        QuienMandoDocumento = ReadString(reader, 8),
        DocumentoAdjunto = ReadString(reader, 9)
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
        EstadoDocumento = string.IsNullOrEmpty(ReadString(reader, 15)) ? "Correcto" : ReadString(reader, 15),
        ImesModelId = ReadString(reader, 16),
        WorkOrderBe = ReadString(reader, 17),
        LineaBe = ReadString(reader, 18)
    };

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection(_connectionString);

        // Sin red o VPN la conexión tarda mucho en fallar, se corta a los pocos segundos
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

        await openTask; // Si falló de verdad, sale aquí

        using var pragma = connection.CreateCommand();
        // Espera hasta 5 s si otra PC tiene el archivo bloqueado
        pragma.CommandText = "PRAGMA busy_timeout = 5000;";
        await pragma.ExecuteNonQueryAsync();

        return connection;
    }
}
