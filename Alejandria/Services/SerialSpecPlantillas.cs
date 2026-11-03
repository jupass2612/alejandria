using System.Text.RegularExpressions;
using Alejandria.Models;

namespace Alejandria.Services;

// Plantilla de MODELS_SERIALNUMBERSPECS por cliente
public class SerialSpecPlantilla
{
    public string Nombre { get; init; } = string.Empty;

    // Filas o el motivo por el que el serial no sirve
    public Func<string, (List<SpecPreviewFila> Filas, string? Error)> Previsualizar { get; init; } = _ => (new(), null);

    // INSERT con el serial y el MODELID
    public Func<string, int, string> GenerarSql { get; init; } = (_, _) => string.Empty;

    public override string ToString() => Nombre;
}

public static class SerialSpecPlantillas
{
    public static IReadOnlyList<SerialSpecPlantilla> Todas { get; } = new[] { Tesla(), Gm() };

    // Escapa la comilla simple para SQL
    private static string Sql(string serial) => serial.Replace("'", "''");

    // Tesla: posiciones fijas con SUBSTR
    private static SerialSpecPlantilla Tesla()
    {
        // (descripción, inicio, longitud, dinámico, guarda valor)
        var segmentos = new (string Desc, int Inicio, int Len, bool Dinamico, bool GuardaValor)[]
        {
            ("SMTPartNumber", 1, 13, false, true),
            ("LineNumber", 23, 1, true, true),
            ("JulianDate", 20, 3, true, false),
            ("Year", 18, 2, true, false),
            ("ParentNumber", 24, 3, true, false),
            ("ChildNumber", 27, 2, true, false),
            ("Queretaro Plant", 15, 3, false, true)
        };

        var largoMinimo = segmentos.Max(s => s.Inicio + s.Len - 1);

        return new SerialSpecPlantilla
        {
            Nombre = "Tesla",
            Previsualizar = serial =>
            {
                if (serial.Length < largoMinimo)
                {
                    return (new(), $"El serial trae {serial.Length} caracteres y la plantilla Tesla necesita al menos {largoMinimo}.");
                }

                var filas = segmentos.Select(s => new SpecPreviewFila
                {
                    ValueDescription = s.Desc,
                    StartPosition = s.Inicio,
                    ExpectedCharacterLen = s.Len,
                    IsDynamicValue = s.Dinamico,
                    FixedCharacterValue = s.GuardaValor ? serial.Substring(s.Inicio - 1, s.Len) : string.Empty
                }).ToList();

                return (filas, null);
            },
            GenerarSql = (serial, modelId) => $"""
                INSERT INTO IMES.MODELS_SERIALNUMBERSPECS
                  (ID, MODELID, VALUEDESCRIPTION, STARTPOSITION, EXPECTEDCHARACTERLEN, ISDYNAMICVALUE, FIXEDCHARACTERVALUE)
                WITH r AS (
                  SELECT
                    '{Sql(serial)}' AS sn,
                    {modelId} AS modelid,
                    (SELECT NVL(MAX(ID), 0) FROM IMES.MODELS_SERIALNUMBERSPECS) AS base_id
                  FROM dual
                )
                SELECT base_id + 1, modelid, 'SMTPartNumber',    1, 13, 0, SUBSTR(sn, 1, 13)          FROM r UNION ALL
                SELECT base_id + 2, modelid, 'LineNumber',      23,  1, 1, SUBSTR(sn, 23, 1)          FROM r UNION ALL
                SELECT base_id + 3, modelid, 'JulianDate',      20,  3, 1, CAST(NULL AS VARCHAR2(50)) FROM r UNION ALL
                SELECT base_id + 4, modelid, 'Year',            18,  2, 1, CAST(NULL AS VARCHAR2(50)) FROM r UNION ALL
                SELECT base_id + 5, modelid, 'ParentNumber',    24,  3, 1, CAST(NULL AS VARCHAR2(50)) FROM r UNION ALL
                SELECT base_id + 6, modelid, 'ChildNumber',     27,  2, 1, CAST(NULL AS VARCHAR2(50)) FROM r UNION ALL
                SELECT base_id + 7, modelid, 'Queretaro Plant', 15,  3, 0, SUBSTR(sn, 15, 3)          FROM r;
                """
        };
    }

    // GM: código 2D con separadores, misma regex que en Oracle
    private static SerialSpecPlantilla Gm()
    {
        const string patron =
            @"^\[\)>[^A-Za-z0-9]*(06)[^A-Za-z0-9]*(Y[A-Za-z0-9]{14})[^A-Za-z0-9]*(P[A-Za-z0-9]{8})[^A-Za-z0-9]*(12V[A-Za-z0-9]{9})[^A-Za-z0-9]*(T[A-Za-z0-9]{16})[^A-Za-z0-9]*(1P[A-Za-z0-9-]{11})[^A-Za-z0-9]*(.*)$";

        // (descripción, inicio, longitud, dinámico, grupo de la regex)
        var segmentos = new (string Desc, int Inicio, int Len, bool Dinamico, int Grupo)[]
        {
            ("BarcodeIdentifier", 5, 2, false, 1),
            ("compressed vpps", 8, 15, false, 2),
            ("GM Part Number", 24, 9, false, 3),
            ("Supplier Duns", 34, 12, false, 4),
            ("Traceability", 47, 17, true, 5),
            ("BCS Part Number", 65, 13, false, 6),
            ("Suffix", 79, 7, true, 7)
        };

        return new SerialSpecPlantilla
        {
            Nombre = "GM",
            Previsualizar = serial =>
            {
                var match = Regex.Match(serial, patron, RegexOptions.Singleline);
                if (!match.Success)
                {
                    return (new(), "El serial no tiene el formato GM: [)>06, Y + 14, P + 8, 12V + 9, T + 16, 1P + 11 y el sufijo.");
                }

                var filas = segmentos.Select(s =>
                {
                    var valor = match.Groups[s.Grupo].Value;

                    // Sufijo solo con letras y números
                    if (s.Grupo == 7)
                    {
                        valor = Regex.Replace(valor, "[^A-Za-z0-9]", string.Empty);
                    }

                    return new SpecPreviewFila
                    {
                        ValueDescription = s.Desc,
                        StartPosition = s.Inicio,
                        ExpectedCharacterLen = s.Len,
                        IsDynamicValue = s.Dinamico,
                        FixedCharacterValue = valor
                    };
                }).ToList();

                return (filas, null);
            },
            GenerarSql = (serial, modelId) => $$"""
                INSERT INTO IMES.MODELS_SERIALNUMBERSPECS
                  (ID, MODELID, VALUEDESCRIPTION, STARTPOSITION, EXPECTEDCHARACTERLEN, ISDYNAMICVALUE, FIXEDCHARACTERVALUE)
                WITH r AS (
                  SELECT
                    '{{Sql(serial)}}' AS sn,
                    {{modelId}} AS modelid,
                    (SELECT NVL(MAX(ID), 0) FROM IMES.MODELS_SERIALNUMBERSPECS) AS base_id,
                    '^\[\)>[^[:alnum:]]*(06)[^[:alnum:]]*(Y[[:alnum:]]{14})[^[:alnum:]]*(P[[:alnum:]]{8})[^[:alnum:]]*(12V[[:alnum:]]{9})[^[:alnum:]]*(T[[:alnum:]]{16})[^[:alnum:]]*(1P[[:alnum:]-]{11})[^[:alnum:]]*(.*)$' AS pat
                  FROM dual
                ),
                g AS (
                  SELECT modelid, base_id,
                         REGEXP_SUBSTR(sn, pat, 1, 1, NULL, 1) AS f1,
                         REGEXP_SUBSTR(sn, pat, 1, 1, NULL, 2) AS f2,
                         REGEXP_SUBSTR(sn, pat, 1, 1, NULL, 3) AS f3,
                         REGEXP_SUBSTR(sn, pat, 1, 1, NULL, 4) AS f4,
                         REGEXP_SUBSTR(sn, pat, 1, 1, NULL, 5) AS f5,
                         REGEXP_SUBSTR(sn, pat, 1, 1, NULL, 6) AS f6,
                         REGEXP_REPLACE(REGEXP_SUBSTR(sn, pat, 1, 1, NULL, 7), '[^[:alnum:]]', '') AS f7
                  FROM r
                )
                SELECT base_id + 1, modelid, 'BarcodeIdentifier',  5,  2, 0, f1 FROM g UNION ALL
                SELECT base_id + 2, modelid, 'compressed vpps',    8, 15, 0, f2 FROM g UNION ALL
                SELECT base_id + 3, modelid, 'GM Part Number',    24,  9, 0, f3 FROM g UNION ALL
                SELECT base_id + 4, modelid, 'Supplier Duns',     34, 12, 0, f4 FROM g UNION ALL
                SELECT base_id + 5, modelid, 'Traceability',      47, 17, 1, f5 FROM g UNION ALL
                SELECT base_id + 6, modelid, 'BCS Part Number',   65, 13, 0, f6 FROM g UNION ALL
                SELECT base_id + 7, modelid, 'Suffix',            79,  7, 1, f7 FROM g;
                """
        };
    }
}
