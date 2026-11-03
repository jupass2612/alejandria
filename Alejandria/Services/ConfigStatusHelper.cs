using Alejandria.Models;

namespace Alejandria.Services;

// Estatus y cómo se combinan: Bloqueado gana, todo completo = Terminado, algo = En Proceso, nada = Pendiente.
// El modelo no llega a Terminado sin PDF
public static class ConfigStatusHelper
{
    public const string Pendiente = "Pendiente";
    public const string EnProceso = "En Proceso";
    public const string Terminado = "Terminado";
    public const string Bloqueado = "Bloqueado por Documento Malo";
    public const string NoAplica = "No Aplica";

    // Estado del documento, aparte de los campos de BD
    public const string DocumentoCorrecto = "Correcto";
    public const string DocumentoPendienteCorrecciones = "Pendiente a correcciones";

    public static IReadOnlyList<string> EstadoDocumentoOptions { get; } =
        new[] { DocumentoCorrecto, DocumentoPendienteCorrecciones };

    // Si alguna área tiene el documento pendiente (no afecta el avance)
    public static bool TieneDocumentoPendienteCorreccion(IEnumerable<DetalleConfiguracion>? detalles) =>
        (detalles ?? Enumerable.Empty<DetalleConfiguracion>()).Any(d => d.EstadoDocumento == DocumentoPendienteCorrecciones);

    // Responsables
    public static readonly string[] Responsables =
    {
        "Juan Pablo Solis Sanchez",
        "Roberto Uriel Martinez",
        "Cecilia Saldivar",
        "Reynaldo Barrera"
    };

    // Nombre como está en Responsables; los viejos cortos se empatan por inicio
    public static string NormalizarResponsable(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return string.Empty;
        }

        var limpio = nombre.Trim();
        return Responsables.FirstOrDefault(r => string.Equals(r, limpio, StringComparison.OrdinalIgnoreCase))
            ?? Responsables.FirstOrDefault(r => r.StartsWith(limpio + " ", StringComparison.OrdinalIgnoreCase))
            ?? limpio;
    }

    // Opciones de los campos de BD
    public static IReadOnlyList<string> BdStatusOptions { get; } =
        new[] { Pendiente, EnProceso, NoAplica }.Concat(Responsables).ToArray();

    // Opciones de PartLineBomMaintenance
    public static IReadOnlyList<string> PlataformaStatusOptions { get; } =
        new[] { Pendiente, EnProceso, NoAplica }.Concat(Responsables).ToArray();

    public static bool IsCompleted(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value == Terminado || value == NoAplica || Responsables.Contains(value));

    public static bool IsBlocked(string? value) => value == Bloqueado;

    public static bool IsFilled(string? value) => !string.IsNullOrWhiteSpace(value);

    // Junta varios valores de campo en un estatus
    public static string Aggregate(params string?[] values)
    {
        if (values.Any(IsBlocked))
        {
            return Bloqueado;
        }
        if (values.All(IsCompleted))
        {
            return Terminado;
        }
        if (values.Any(v => !string.IsNullOrWhiteSpace(v)))
        {
            return EnProceso;
        }
        return Pendiente;
    }

    // Junta estatus ya calculados
    public static string Combine(params string[] statuses)
    {
        if (statuses.Any(s => s == Bloqueado))
        {
            return Bloqueado;
        }
        if (statuses.All(s => s == Terminado))
        {
            return Terminado;
        }
        if (statuses.All(s => s == Pendiente))
        {
            return Pendiente;
        }
        return EnProceso;
    }

    // SMT y SMT-BE usan Ruta, BE y TLA Flujo (mismo ROUTE_NAME)
    public static bool EsAreaSmt(string area) =>
        string.Equals(area, "SMT", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(area, "SMT-BE", StringComparison.OrdinalIgnoreCase);

    public static string AggregatePlataforma(DetalleConfiguracion detalle)
    {
        var estatusRutaOFlujo = EsAreaSmt(detalle.Area)
            ? (IsFilled(detalle.Ruta) ? Terminado : Pendiente)
            : (IsFilled(detalle.Flujo) ? Terminado : Pendiente);

        return Aggregate(
            IsFilled(detalle.WorkOrder) ? Terminado : Pendiente,
            estatusRutaOFlujo,
            IsFilled(detalle.Linea) ? Terminado : Pendiente,
            detalle.PartLineBomMaintenance);
    }

    // Campos que cuentan para el conteo por persona (Ruta/Flujo vienen de iMES, no cuentan)
    public static IEnumerable<string> CamposDeEstatus(DetalleConfiguracion detalle)
    {
        yield return detalle.ImesModelsParents;
        yield return detalle.ImesModelsMaster;
        yield return detalle.ImesModelsSerialNumberSpecs;
        yield return detalle.PartLineBomMaintenance;
    }

    // Resumen: BD, Plataforma, General, fecha límite y retraso
    public readonly record struct ModeloResumen(
        string EstatusBd,
        string EstatusPlataforma,
        string EstatusGeneral,
        DateTime FechaLimite,
        int DiasRetraso);

    // Resumen de un área
    public static ModeloResumen ComputeResumen(ModelRecord modelo, DetalleConfiguracion detalle)
    {
        var estatusBd = Aggregate(detalle.ImesModelsParents, detalle.ImesModelsMaster, detalle.ImesModelsSerialNumberSpecs, detalle.ImesModelsSerialNumberSpecsPacking);
        var estatusPlataforma = AggregatePlataforma(detalle);
        var estatusGeneral = Combine(estatusBd, estatusPlataforma);

        var fechaLimite = BusinessDayHelper.AddBusinessDays(modelo.FechaSolicitud, 5);
        var terminado = estatusGeneral == Terminado;
        var diasRetraso = BusinessDayHelper.DiasDeRetraso(fechaLimite, terminado);

        return new ModeloResumen(estatusBd, estatusPlataforma, estatusGeneral, fechaLimite, diasRetraso);
    }

    // Detalle del área principal, vacío si no hay
    public static DetalleConfiguracion ObtenerDetallePrincipal(ModelRecord modelo, IEnumerable<DetalleConfiguracion>? detalles)
    {
        var areaPrincipal = modelo.AreaPrincipal;
        var detalle = detalles?.FirstOrDefault(d => d.Area == areaPrincipal);
        return detalle ?? new DetalleConfiguracion { ModeloId = modelo.Id, Area = areaPrincipal };
    }

    // Resumen de todo el modelo; Terminado solo si todas las áreas lo están
    public static ModeloResumen ComputeResumenModelo(ModelRecord modelo, IEnumerable<DetalleConfiguracion>? detallesDelModelo)
    {
        var fechaLimite = BusinessDayHelper.AddBusinessDays(modelo.FechaSolicitud, 5);
        var areas = modelo.AreasDisponibles;

        if (areas.Count == 0)
        {
            // No debería pasar
            return new ModeloResumen(Pendiente, Pendiente, Pendiente, fechaLimite, 0);
        }

        var detallesPorArea = (detallesDelModelo ?? Enumerable.Empty<DetalleConfiguracion>())
            .ToDictionary(d => d.Area, d => d);

        var estatusBdPorArea = new List<string>();
        var estatusPlataformaPorArea = new List<string>();
        var estatusGeneralPorArea = new List<string>();

        foreach (var area in areas)
        {
            detallesPorArea.TryGetValue(area, out var detalle);
            detalle ??= new DetalleConfiguracion { ModeloId = modelo.Id, Area = area };

            var resumenArea = ComputeResumen(modelo, detalle);
            estatusBdPorArea.Add(resumenArea.EstatusBd);
            estatusPlataformaPorArea.Add(resumenArea.EstatusPlataforma);
            estatusGeneralPorArea.Add(resumenArea.EstatusGeneral);
        }

        var estatusBdConsolidado = Combine(estatusBdPorArea.ToArray());
        var estatusPlataformaConsolidado = Combine(estatusPlataformaPorArea.ToArray());
        var estatusGeneralConsolidado = Combine(estatusGeneralPorArea.ToArray());

        // Sin PDF no cuenta como Terminado
        if (estatusGeneralConsolidado == Terminado && !modelo.TieneDocumentoAdjunto)
        {
            estatusGeneralConsolidado = Pendiente;
        }

        var terminado = estatusGeneralConsolidado == Terminado;
        var diasRetraso = BusinessDayHelper.DiasDeRetraso(fechaLimite, terminado);

        return new ModeloResumen(estatusBdConsolidado, estatusPlataformaConsolidado, estatusGeneralConsolidado, fechaLimite, diasRetraso);
    }

    // Estatus de un campo: responsable o No Aplica = Terminado
    public static string EstatusDeCampo(string? valor) =>
        IsCompleted(valor) ? Terminado
        : valor == EnProceso ? EnProceso
        : Pendiente;

    // Estatus de los 4 pasos de un área
    public static string[] EstatusPasos(DetalleConfiguracion detalle) => new[]
    {
        EstatusDeCampo(detalle.ImesModelsParents),
        EstatusDeCampo(detalle.ImesModelsMaster),
        Combine(EstatusDeCampo(detalle.ImesModelsSerialNumberSpecs), EstatusDeCampo(detalle.ImesModelsSerialNumberSpecsPacking)),
        AggregatePlataforma(detalle)
    };

    // De todo el modelo: Terminado solo si lo está en todas las áreas
    public static string[] EstatusPasosModelo(ModelRecord modelo, IEnumerable<DetalleConfiguracion>? detalles)
    {
        var porArea = (detalles ?? Enumerable.Empty<DetalleConfiguracion>()).ToDictionary(d => d.Area, d => d);
        var areas = modelo.AreasDisponibles;
        if (areas.Count == 0)
        {
            return new[] { Pendiente, Pendiente, Pendiente, Pendiente };
        }

        var estatusPorArea = areas
            .Select(a => EstatusPasos(porArea.TryGetValue(a, out var d) ? d : new DetalleConfiguracion { ModeloId = modelo.Id, Area = a }))
            .ToList();

        return Enumerable.Range(0, 4)
            .Select(i => Combine(estatusPorArea.Select(e => e[i]).ToArray()))
            .ToArray();
    }
}
