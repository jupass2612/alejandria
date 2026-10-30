using Alejandria.Models;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Alejandria.Services;

// Lectura y escritura real de las 3 tablas de iMES — esto es lo que
// hace que Alejandría configure directamente en iMES, en vez de solo
// llevar el seguimiento de que alguien más ya lo hizo a mano.
//
// El flujo pensado al dar de alta un modelo:
//  1. Buscar en MODELS_PARENTS por nombre parecido — si hay coincidencias,
//     elegir una existente; si no, se capturan los 6 campos y se crea.
//  2. Buscar en MODELS_MASTER por los primeros 8 caracteres del número de
//     parte — si coincide, se puede basar el nuevo registro en ese (se
//     copian sus valores como punto de partida, editable antes de guardar).
//  3. Si el MASTER se basó en uno existente, se ofrecen los
//     MODELS_SERIALNUMBERSPECS de ESE modelo (el que sirvió de base) para
//     copiarlos al nuevo, en vez de capturarlos desde cero.
public class ImesOracleModelService
{
    private readonly ImesOracleEnvironment _entorno;

    public ImesOracleModelService(ImesOracleEnvironment entorno)
    {
        _entorno = entorno;
    }

    // ---- MODELS_PARENTS ----

    // Trae todos los "parents" activos y los ordena por qué tan parecido
    // es su nombre al buscado (de más a menos parecido) — para poder
    // sugerir "¿te refieres a uno de estos?" en vez de forzar un nombre exacto.
    public async Task<List<(ImesParentModel Parent, double Similitud)>> BuscarParentsSimilaresAsync(string nombreBuscado, int maxResultados = 5)
    {
        var todos = await ObtenerTodosLosParentsAsync();

        return todos
            .Select(p => (Parent: p, Similitud: CalcularSimilitud(nombreBuscado, p.ParentModelName)))
            .Where(x => x.Similitud > 0.4)
            .OrderByDescending(x => x.Similitud)
            .Take(maxResultados)
            .ToList();
    }

    public async Task<List<ImesParentModel>> ObtenerTodosLosParentsAsync()
    {
        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ID, PARENTMODELNAME, PARTNUMBER, FAMILY, VALIDATEFLOW, VALIDATELASTOPRESULT, FLOWFWDVALIDATION, ISACTIVE
            FROM IMES.MODELS_PARENTS
            WHERE NVL(ISACTIVE, 0) = 1
            ORDER BY PARENTMODELNAME
            """;

        await using var reader = await command.ExecuteReaderAsync();
        var resultado = new List<ImesParentModel>();
        while (await reader.ReadAsync())
        {
            resultado.Add(LeerParent(reader));
        }

        return resultado;
    }

    // Crea un "parent" nuevo y regresa su Id (generado por la secuencia).
    // Trae un Parent por su Id — para cuando el modelo ya tiene uno
    // recordado (ver ModelRecord.OracleParentId) y solo hace falta
    // confirmar que sigue existiendo y mostrar sus datos actuales.
    public async Task<ImesParentModel?> ObtenerParentPorIdAsync(int id)
    {
        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ID, PARENTMODELNAME, PARTNUMBER, FAMILY, VALIDATEFLOW, VALIDATELASTOPRESULT, FLOWFWDVALIDATION, ISACTIVE
            FROM IMES.MODELS_PARENTS
            WHERE ID = :id
            """;
        command.Parameters.Add("id", OracleDbType.Int32).Value = id;

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? LeerParent(reader) : null;
    }

    public async Task<int> CrearParentAsync(ImesParentModel parent)
    {
        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO IMES.MODELS_PARENTS
                (PARENTMODELNAME, PARTNUMBER, FAMILY, VALIDATEFLOW, VALIDATELASTOPRESULT, FLOWFWDVALIDATION, ISACTIVE)
            VALUES
                (:parentModelName, :partNumber, :family, :validateFlow, :validateLastOpResult, :flowFwdValidation, :isActive)
            RETURNING ID INTO :id
            """;

        command.Parameters.Add("parentModelName", OracleDbType.Varchar2).Value = ToDb(parent.ParentModelName);
        command.Parameters.Add("partNumber", OracleDbType.Varchar2).Value = ToDb(parent.PartNumber);
        command.Parameters.Add("family", OracleDbType.Varchar2).Value = ToDb(parent.Family);
        command.Parameters.Add("validateFlow", OracleDbType.Int32).Value = parent.ValidateFlow ? 1 : 0;
        command.Parameters.Add("validateLastOpResult", OracleDbType.Int32).Value = parent.ValidateLastOpResult ? 1 : 0;
        command.Parameters.Add("flowFwdValidation", OracleDbType.Int32).Value = parent.FlowFwdValidation ? 1 : 0;
        command.Parameters.Add("isActive", OracleDbType.Int32).Value = parent.IsActive ? 1 : 0;

        var idParam = command.Parameters.Add("id", OracleDbType.Int32);
        idParam.Direction = System.Data.ParameterDirection.Output;

        await command.ExecuteNonQueryAsync();
        return Convert.ToInt32(((OracleDecimal)idParam.Value).Value);
    }

    // ---- MODELS_MASTER ----

    // ---- MODELS_MASTER ----

    // Todos los modelos activos, para "Historial de Modelos" — hasta
    // 500 a la vez (nada más para ver, no hace falta traer todo si son
    // miles de filas).
    public async Task<List<ImesMasterModel>> ObtenerTodosLosMastersAsync()
    {
        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ID, MODELNAME, MODELDESC, PARENTMODELID, MODELPARTNUMBER, MODELQRPARTNUMBER, MODELPRODUCTID,
                   PCBSPERPANEL, UICOLOR, SIMULATEINTERLOCK, VALIDATEINTERLOCK, SERIALNUMBERBYMES,
                   SERIALNUMBERCREATIONSP, ISACTIVE, FORCESECONDARYSERIALNUMBER, MODELPARTNUMBER2, FGPARTNUMBER, HANDLINGLOGIC
            FROM IMES.MODELS_MASTER
            WHERE ROWNUM <= 500
            ORDER BY ID DESC
            """;

        await using var reader = await command.ExecuteReaderAsync();
        var resultado = new List<ImesMasterModel>();
        while (await reader.ReadAsync())
        {
            resultado.Add(LeerMaster(reader));
        }

        return resultado;
    }

    // Modelos cuyo número de parte coincide en los primeros 8 caracteres
    // — típicamente eso significa "es una variante del mismo modelo
    // base" (ej. distinto sufijo de revisión), buen candidato para
    // basarse en él en vez de capturar todo desde cero.
    public async Task<List<ImesMasterModel>> BuscarMasterPorPrefijoAsync(string numeroDeParte, int longitudPrefijo = 8)
    {
        if (string.IsNullOrWhiteSpace(numeroDeParte))
        {
            return new List<ImesMasterModel>();
        }

        var prefijo = numeroDeParte.Length >= longitudPrefijo ? numeroDeParte[..longitudPrefijo] : numeroDeParte;

        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ID, MODELNAME, MODELDESC, PARENTMODELID, MODELPARTNUMBER, MODELQRPARTNUMBER, MODELPRODUCTID,
                   PCBSPERPANEL, UICOLOR, SIMULATEINTERLOCK, VALIDATEINTERLOCK, SERIALNUMBERBYMES,
                   SERIALNUMBERCREATIONSP, ISACTIVE, FORCESECONDARYSERIALNUMBER, MODELPARTNUMBER2, FGPARTNUMBER, HANDLINGLOGIC
            FROM IMES.MODELS_MASTER
            WHERE SUBSTR(MODELPARTNUMBER, 1, :longitudPrefijo) = :prefijo
              AND NVL(ISACTIVE, 0) = 1
            ORDER BY MODELPARTNUMBER
            """;

        command.Parameters.Add("longitudPrefijo", OracleDbType.Int32).Value = longitudPrefijo;
        command.Parameters.Add("prefijo", OracleDbType.Varchar2).Value = prefijo;

        await using var reader = await command.ExecuteReaderAsync();
        var resultado = new List<ImesMasterModel>();
        while (await reader.ReadAsync())
        {
            resultado.Add(LeerMaster(reader));
        }

        return resultado;
    }

    // Busca un MASTER exacto (mismo número de parte Y mismo modelo padre)
    // — para saber de verdad, contra Oracle, si esta combinación ya se
    // dio de alta, en vez de solo confiar en la marca local.
    public async Task<ImesMasterModel?> BuscarMasterExactoAsync(string numeroDeParte, int parentId)
    {
        if (string.IsNullOrWhiteSpace(numeroDeParte))
        {
            return null;
        }

        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ID, MODELNAME, MODELDESC, PARENTMODELID, MODELPARTNUMBER, MODELQRPARTNUMBER, MODELPRODUCTID,
                   PCBSPERPANEL, UICOLOR, SIMULATEINTERLOCK, VALIDATEINTERLOCK, SERIALNUMBERBYMES,
                   SERIALNUMBERCREATIONSP, ISACTIVE, FORCESECONDARYSERIALNUMBER, MODELPARTNUMBER2, FGPARTNUMBER, HANDLINGLOGIC
            FROM IMES.MODELS_MASTER
            WHERE MODELPARTNUMBER = :numeroDeParte
              AND PARENTMODELID = :parentId
              AND NVL(ISACTIVE, 0) = 1
            """;

        command.Parameters.Add("numeroDeParte", OracleDbType.Varchar2).Value = numeroDeParte;
        command.Parameters.Add("parentId", OracleDbType.Int32).Value = parentId;

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? LeerMaster(reader) : null;
    }

    public async Task<int> CrearMasterAsync(ImesMasterModel master)
    {
        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO IMES.MODELS_MASTER
                (MODELNAME, MODELDESC, PARENTMODELID, MODELPARTNUMBER, MODELQRPARTNUMBER, MODELPRODUCTID,
                 PCBSPERPANEL, UICOLOR, SIMULATEINTERLOCK, VALIDATEINTERLOCK, SERIALNUMBERBYMES,
                 SERIALNUMBERCREATIONSP, ISACTIVE, FORCESECONDARYSERIALNUMBER, MODELPARTNUMBER2, FGPARTNUMBER, HANDLINGLOGIC)
            VALUES
                (:modelName, :modelDesc, :parentModelId, :modelPartNumber, :modelQrPartNumber, :modelProductId,
                 :pcbsPerPanel, :uiColor, :simulateInterlock, :validateInterlock, :serialNumberByMes,
                 :serialNumberCreationSp, :isActive, :forceSecondarySerialNumber, :modelPartNumber2, :fgPartNumber, :handlingLogic)
            RETURNING ID INTO :id
            """;

        command.Parameters.Add("modelName", OracleDbType.Varchar2).Value = ToDb(master.ModelName);
        command.Parameters.Add("modelDesc", OracleDbType.Varchar2).Value = ToDb(master.ModelDesc);
        command.Parameters.Add("parentModelId", OracleDbType.Int32).Value = master.ParentModelId.HasValue ? master.ParentModelId.Value : DBNull.Value;
        command.Parameters.Add("modelPartNumber", OracleDbType.Varchar2).Value = ToDb(master.ModelPartNumber);
        command.Parameters.Add("modelQrPartNumber", OracleDbType.Varchar2).Value = ToDb(master.ModelQrPartNumber);
        command.Parameters.Add("modelProductId", OracleDbType.Varchar2).Value = ToDb(master.ModelProductId);
        command.Parameters.Add("pcbsPerPanel", OracleDbType.Int32).Value = master.PcbsPerPanel.HasValue ? master.PcbsPerPanel.Value : DBNull.Value;
        command.Parameters.Add("uiColor", OracleDbType.Varchar2).Value = ToDb(master.UiColor);
        command.Parameters.Add("simulateInterlock", OracleDbType.Int32).Value = master.SimulateInterlock ? 1 : 0;
        command.Parameters.Add("validateInterlock", OracleDbType.Int32).Value = master.ValidateInterlock ? 1 : 0;
        command.Parameters.Add("serialNumberByMes", OracleDbType.Int32).Value = master.SerialNumberByMes ? 1 : 0;
        command.Parameters.Add("serialNumberCreationSp", OracleDbType.Varchar2).Value = ToDb(master.SerialNumberCreationSp);
        command.Parameters.Add("isActive", OracleDbType.Int32).Value = master.IsActive ? 1 : 0;
        command.Parameters.Add("forceSecondarySerialNumber", OracleDbType.Int32).Value = master.ForceSecondarySerialNumber ? 1 : 0;
        command.Parameters.Add("modelPartNumber2", OracleDbType.Varchar2).Value = ToDb(master.ModelPartNumber2);
        command.Parameters.Add("fgPartNumber", OracleDbType.Varchar2).Value = ToDb(master.FgPartNumber);
        command.Parameters.Add("handlingLogic", OracleDbType.Varchar2).Value = ToDb(master.HandlingLogic);

        var idParam = command.Parameters.Add("id", OracleDbType.Int32);
        idParam.Direction = System.Data.ParameterDirection.Output;

        await command.ExecuteNonQueryAsync();
        return Convert.ToInt32(((OracleDecimal)idParam.Value).Value);
    }

    // ---- MODELS_SERIALNUMBERSPECS ----

    // ---- MODELS_SERIALNUMBERSPECS ----

    // Todos los segmentos de serial, para "Historial de Modelos" — hasta
    // 500 a la vez.
    public async Task<List<ImesSerialNumberSpec>> ObtenerTodosLosSerialSpecsAsync()
    {
        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ID, MODELID, VALUEDESCRIPTION, STARTPOSITION, EXPECTEDCHARACTERLEN, ISDYNAMICVALUE, FIXEDCHARACTERVALUE
            FROM IMES.MODELS_SERIALNUMBERSPECS
            WHERE ROWNUM <= 500
            ORDER BY MODELID DESC, STARTPOSITION
            """;

        await using var reader = await command.ExecuteReaderAsync();
        var resultado = new List<ImesSerialNumberSpec>();
        while (await reader.ReadAsync())
        {
            resultado.Add(LeerSerialSpec(reader));
        }

        return resultado;
    }

    // Los segmentos del serial de UN modelo — se usa tanto para mostrar
    // los de un modelo ya existente (para copiarlos) como, más adelante,
    // los del modelo recién creado.
    public async Task<List<ImesSerialNumberSpec>> ObtenerSerialSpecsDeModeloAsync(int modelId)
    {
        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ID, MODELID, VALUEDESCRIPTION, STARTPOSITION, EXPECTEDCHARACTERLEN, ISDYNAMICVALUE, FIXEDCHARACTERVALUE
            FROM IMES.MODELS_SERIALNUMBERSPECS
            WHERE MODELID = :modelId
            ORDER BY STARTPOSITION
            """;
        command.Parameters.Add("modelId", OracleDbType.Int32).Value = modelId;

        await using var reader = await command.ExecuteReaderAsync();
        var resultado = new List<ImesSerialNumberSpec>();
        while (await reader.ReadAsync())
        {
            resultado.Add(LeerSerialSpec(reader));
        }

        return resultado;
    }

    // Crea los segmentos de serial de un modelo nuevo — normalmente son
    // los de un modelo existente que se copiaron como base (ver
    // ObtenerSerialSpecsDeModeloAsync) y aquí se guardan ya con el
    // MODELID del modelo nuevo.
    public async Task CrearSerialSpecsAsync(int modelIdNuevo, IEnumerable<ImesSerialNumberSpec> specs)
    {
        await using var connection = new OracleConnection(_entorno.ConnectionString);
        await connection.OpenAsync();

        foreach (var spec in specs)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO IMES.MODELS_SERIALNUMBERSPECS
                    (MODELID, VALUEDESCRIPTION, STARTPOSITION, EXPECTEDCHARACTERLEN, ISDYNAMICVALUE, FIXEDCHARACTERVALUE)
                VALUES
                    (:modelId, :valueDescription, :startPosition, :expectedCharacterLen, :isDynamicValue, :fixedCharacterValue)
                """;

            command.Parameters.Add("modelId", OracleDbType.Int32).Value = modelIdNuevo;
            command.Parameters.Add("valueDescription", OracleDbType.Varchar2).Value = ToDb(spec.ValueDescription);
            command.Parameters.Add("startPosition", OracleDbType.Int32).Value = spec.StartPosition;
            command.Parameters.Add("expectedCharacterLen", OracleDbType.Int32).Value = spec.ExpectedCharacterLen;
            command.Parameters.Add("isDynamicValue", OracleDbType.Int32).Value = spec.IsDynamicValue ? 1 : 0;
            command.Parameters.Add("fixedCharacterValue", OracleDbType.Varchar2).Value = ToDb(spec.FixedCharacterValue);

            await command.ExecuteNonQueryAsync();
        }
    }

    // ---- Lectura de filas ----

    private static ImesParentModel LeerParent(OracleDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ParentModelName = ReadString(reader, 1),
        PartNumber = ReadString(reader, 2),
        Family = ReadString(reader, 3),
        ValidateFlow = ReadBool(reader, 4),
        ValidateLastOpResult = ReadBool(reader, 5),
        FlowFwdValidation = ReadBool(reader, 6),
        IsActive = ReadBool(reader, 7)
    };

    private static ImesMasterModel LeerMaster(OracleDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ModelName = ReadString(reader, 1),
        ModelDesc = ReadString(reader, 2),
        ParentModelId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
        ModelPartNumber = ReadString(reader, 4),
        ModelQrPartNumber = ReadString(reader, 5),
        ModelProductId = ReadString(reader, 6),
        PcbsPerPanel = reader.IsDBNull(7) ? null : reader.GetInt32(7),
        UiColor = ReadString(reader, 8),
        SimulateInterlock = ReadBool(reader, 9),
        ValidateInterlock = ReadBool(reader, 10),
        SerialNumberByMes = ReadBool(reader, 11),
        SerialNumberCreationSp = ReadString(reader, 12),
        IsActive = ReadBool(reader, 13),
        ForceSecondarySerialNumber = ReadBool(reader, 14),
        ModelPartNumber2 = ReadString(reader, 15),
        FgPartNumber = ReadString(reader, 16),
        HandlingLogic = ReadString(reader, 17)
    };

    private static ImesSerialNumberSpec LeerSerialSpec(OracleDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        ModelId = reader.GetInt32(1),
        ValueDescription = ReadString(reader, 2),
        StartPosition = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
        ExpectedCharacterLen = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
        IsDynamicValue = ReadBool(reader, 5),
        FixedCharacterValue = ReadString(reader, 6)
    };

    private static string ReadString(OracleDataReader reader, int index) => reader.IsDBNull(index) ? string.Empty : reader.GetString(index);
    private static bool ReadBool(OracleDataReader reader, int index) => !reader.IsDBNull(index) && reader.GetInt32(index) == 1;
    private static object ToDb(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    // ---- Similitud de nombres ----

    // 1.0 = idénticos, 0.0 = nada que ver. Basado en distancia de
    // Levenshtein normalizada contra la palabra más larga — suficiente
    // para detectar variaciones de mayúsculas, espacios, o pequeñas
    // diferencias de escritura ("Charge Port" vs "Chargeport").
    private static double CalcularSimilitud(string a, string b)
    {
        var x = Normalizar(a);
        var y = Normalizar(b);

        if (x.Length == 0 || y.Length == 0)
        {
            return 0;
        }

        if (x == y)
        {
            return 1;
        }

        var distancia = DistanciaLevenshtein(x, y);
        var maxLen = Math.Max(x.Length, y.Length);
        return 1.0 - (double)distancia / maxLen;
    }

    private static string Normalizar(string texto) =>
        new string(texto.ToLowerInvariant().Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '_').ToArray());

    private static int DistanciaLevenshtein(string a, string b)
    {
        var matriz = new int[a.Length + 1, b.Length + 1];

        for (var i = 0; i <= a.Length; i++) matriz[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) matriz[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var costo = a[i - 1] == b[j - 1] ? 0 : 1;
                matriz[i, j] = Math.Min(
                    Math.Min(matriz[i - 1, j] + 1, matriz[i, j - 1] + 1),
                    matriz[i - 1, j - 1] + costo);
            }
        }

        return matriz[a.Length, b.Length];
    }
}
