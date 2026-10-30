using Alejandria.Models;

namespace Alejandria.Services;

// Constantes de estatus y cómo combinarlos: si algo está Bloqueado, todo
// está Bloqueado; si todo está completo, Terminado; si hay algo pero no
// todo, En Proceso; si no hay nada, Pendiente. El estatus general del
// MODELO completo tampoco llega a Terminado sin el PDF adjunto.
public static class ConfigStatusHelper
{
    public const string Pendiente = "Pendiente";
    public const string EnProceso = "En Proceso";
    public const string Terminado = "Terminado";
    public const string Bloqueado = "Bloqueado por Documento Malo";
    public const string NoAplica = "No Aplica";

    // Estado del documento (PDF) de un área — separado de los campos de
    // BD, ya no es una opción más del dropdown de estatus. Si queda
    // "Pendiente a correcciones", esa área (y por lo tanto el modelo) no
    // puede llegar a Terminado, y en Histórico se habilita "Adjuntar PDF"
    // para poder subir la versión corregida sin tener que usar "Actualizar".
    public const string DocumentoCorrecto = "Correcto";
    public const string DocumentoPendienteCorrecciones = "Pendiente a correcciones";

    public static IReadOnlyList<string> EstadoDocumentoOptions { get; } =
        new[] { DocumentoCorrecto, DocumentoPendienteCorrecciones };

    // True si alguna de las áreas del modelo tiene el documento marcado
    // "Pendiente a correcciones" — no afecta el estatus de avance, solo
    // sirve para saber si hace falta subir una versión corregida del PDF.
    public static bool TieneDocumentoPendienteCorreccion(IEnumerable<DetalleConfiguracion>? detalles) =>
        (detalles ?? Enumerable.Empty<DetalleConfiguracion>()).Any(d => d.EstadoDocumento == DocumentoPendienteCorrecciones);

    // Personas que pueden aparecer como responsables.
    public static readonly string[] Responsables =
    {
        "Juan Pablo Solis Sanchez",
        "Roberto Uriel Martinez",
        "Cecilia Saldivar",
        "Reynaldo Barrera"
    };

    // Opciones para los campos de BD (iMES). "Bloqueado" ya no es una
    // opción aquí — ahora se controla aparte con "Estado del Documento".
    public static IReadOnlyList<string> BdStatusOptions { get; } =
        new[] { Pendiente, EnProceso, NoAplica }.Concat(Responsables).ToArray();

    // Opciones para Flujo y PartLineBomMaintenance. Sin "Bloqueado".
    public static IReadOnlyList<string> PlataformaStatusOptions { get; } =
        new[] { Pendiente, EnProceso, NoAplica }.Concat(Responsables).ToArray();

    public static bool IsCompleted(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value == Terminado || value == NoAplica || Responsables.Contains(value));

    public static bool IsBlocked(string? value) => value == Bloqueado;

    public static bool IsFilled(string? value) => !string.IsNullOrWhiteSpace(value);

    // Combina varios valores de campo (estatus, "No Aplica", o nombre) en un estatus agregado.
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

    // Combina estatus ya calculados (ej. BD + Plataforma, o el mismo estatus de varias áreas).
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

    // Estatus de Plataforma iMES: Work Order/Ruta/Línea + Flujo/PartLineBomMaintenance.
    // SMT le llama "Ruta" al campo que llega de iMES (ROUTE_NAME); BE y
    // TLA le llaman "Flujo" al mismo dato — es el mismo valor, solo
    // cambia cómo se le dice según el área.
    // SMT-BE (área combinada) usa "Ruta" igual que SMT sola.
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

    // Campos de estatus/responsable de un modelo, para el conteo por persona.
    // Ruta/Flujo ya no cuentan aquí: dejaron de ser un estatus que alguien
    // marca a mano — ahora es un dato que llega solo de iMES, igual que
    // Work Order y Línea (que tampoco cuentan aquí).
    public static IEnumerable<string> CamposDeEstatus(DetalleConfiguracion detalle)
    {
        yield return detalle.ImesModelsParents;
        yield return detalle.ImesModelsMaster;
        yield return detalle.ImesModelsSerialNumberSpecs;
        yield return detalle.PartLineBomMaintenance;
    }

    // Resumen de un modelo: estatus BD, Plataforma, General, fecha límite y días de retraso.
    public readonly record struct ModeloResumen(
        string EstatusBd,
        string EstatusPlataforma,
        string EstatusGeneral,
        DateTime FechaLimite,
        int DiasRetraso);

    // Calcula el resumen de un área — punto único de esta lógica.
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

    // Detalle del área principal (SMT > BE > TLA). Vacío si no tiene nada capturado.
    public static DetalleConfiguracion ObtenerDetallePrincipal(ModelRecord modelo, IEnumerable<DetalleConfiguracion>? detalles)
    {
        var areaPrincipal = modelo.AreaPrincipal;
        var detalle = detalles?.FirstOrDefault(d => d.Area == areaPrincipal);
        return detalle ?? new DetalleConfiguracion { ModeloId = modelo.Id, Area = areaPrincipal };
    }

    // Resumen de TODAS las áreas del modelo — solo llega a Terminado
    // cuando todas las áreas lo están. Lo usan Modelos, Estadísticas y los reportes.
    public static ModeloResumen ComputeResumenModelo(ModelRecord modelo, IEnumerable<DetalleConfiguracion>? detallesDelModelo)
    {
        var fechaLimite = BusinessDayHelper.AddBusinessDays(modelo.FechaSolicitud, 5);
        var areas = modelo.AreasDisponibles;

        if (areas.Count == 0)
        {
            // No debería pasar, pero por si acaso:
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

        // Sin el PDF adjunto (el que justifica/evidencia la configuración),
        // el modelo no puede considerarse Terminado aunque todos sus campos
        // ya estén completos — se queda en Pendiente hasta que se adjunte.
        if (estatusGeneralConsolidado == Terminado && !modelo.TieneDocumentoAdjunto)
        {
            estatusGeneralConsolidado = Pendiente;
        }

        var terminado = estatusGeneralConsolidado == Terminado;
        var diasRetraso = BusinessDayHelper.DiasDeRetraso(fechaLimite, terminado);

        return new ModeloResumen(estatusBdConsolidado, estatusPlataformaConsolidado, estatusGeneralConsolidado, fechaLimite, diasRetraso);
    }
}
