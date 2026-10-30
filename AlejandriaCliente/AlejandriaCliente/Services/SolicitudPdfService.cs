using AlejandriaCliente.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace AlejandriaCliente.Services;

// Genera los PDF de solicitud calcados de las plantillas de Word
// originales (tablas con bordes, mismas etiquetas) — así Alejandría los
// puede leer sin tener que adivinar el formato. La única diferencia a
// propósito: el diagrama de flujo (los círculos conectados a mano) se
// reemplaza por la imagen que adjunte quien llena el formulario.
//
// Cada fila mide primero cuánto texto real lleva (etiqueta y valor) y
// calcula su propia altura — en vez de una altura fija — para que nada
// se encime entre renglones ni se corte a la mitad. Esto también ayuda
// a que Alejandría lea el texto limpio, sin caracteres pegados.
public static class SolicitudPdfService
{
    private const double PageWidth = 595; // A4 a 72pt/pulgada
    private const double PageHeight = 842;
    private const double MarginLeft = 40;
    private const double MarginRight = 40;
    private const double MarginBottom = 50;
    private const double FilaAltoMinimo = 24;
    private const double RellenoCelda = 6;

    private class Fonts
    {
        public XFont Titulo { get; } = new("Segoe UI", 14, XFontStyleEx.Bold);
        public XFont Seccion { get; } = new("Segoe UI", 11, XFontStyleEx.Bold);
        public XFont Etiqueta { get; } = new("Segoe UI", 8.5, XFontStyleEx.Bold);
        public XFont Valor { get; } = new("Segoe UI", 8.5, XFontStyleEx.Regular);
        public XFont Pie { get; } = new("Segoe UI", 7.5, XFontStyleEx.Italic);
    }

    private class Cursor
    {
        public required PdfDocument Documento { get; init; }
        public required Fonts Fonts { get; init; }
        public XGraphics Graficos { get; set; } = null!;
        public double Y { get; set; }

        public void NuevaPagina()
        {
            var pagina = Documento.AddPage();
            pagina.Width = PageWidth;
            pagina.Height = PageHeight;
            Graficos = XGraphics.FromPdfPage(pagina);
            Y = 36;
        }

        public void AsegurarEspacio(double alto)
        {
            if (Y + alto > PageHeight - MarginBottom)
            {
                NuevaPagina();
            }
        }

        // Cuántas líneas necesita un texto para caber en un ancho dado,
        // a un tamaño de letra dado — para calcular la altura real de
        // cada celda antes de dibujarla.
        public double AlturaNecesaria(string? texto, XFont fuente, double anchoDisponible)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return FilaAltoMinimo;
            }

            var anchoUtil = Math.Max(anchoDisponible - (RellenoCelda * 2), 20);
            var anchoTexto = Graficos.MeasureString(texto, fuente).Width;
            var lineas = Math.Max(1, Math.Ceiling(anchoTexto / anchoUtil));
            var altoLinea = fuente.Height + 3;
            return Math.Max(FilaAltoMinimo, (lineas * altoLinea) + (RellenoCelda * 2));
        }
    }

    public static void Export(string filePath, SolicitudModelo s)
    {
        using var documento = new PdfDocument();
        documento.Info.Title = $"New model & part number request — {s.NombreModelo}";
        documento.Info.Author = s.NombreSolicitante;

        var c = new Cursor { Documento = documento, Fonts = new Fonts() };
        c.NuevaPagina();

        DrawEncabezado(c, "BCS Unity System: New model & part number request",
            "Please provide the requested information for any field you want to change. If a field is blank MES Team will not make any change to that field");

        DrawSeccionGeneral(c, s.FechaSolicitud, s.NombreSolicitante, s.CorreoSolicitante, s.FechaEsperada,
            "MES Team will need up to 5 working days to add a new model configuration into Unity");

        DrawTituloSeccion(c, "2. Model Information");
        var sapPartNumber = JuntarConEtiqueta(("FG", s.SapPartNumberFG), ("PCBA", s.SapPartNumberPCBA), ("T&C", s.SapPartNumberTC));
        var seriales = string.Join("; ", s.Seriales.Where(x => !string.IsNullOrWhiteSpace(x.Valor)).Select(x => string.IsNullOrWhiteSpace(x.Etiqueta) ? x.Valor : $"{x.Etiqueta}: {x.Valor}"));
        var clientPartNumber = JuntarConEtiqueta(("FG", s.ClientPartNumberFG), ("PCBA", s.ClientPartNumberPCBA));
        DrawFilaEtiquetaValor(c, "Model Name", s.NombreModelo);
        DrawFilaEtiquetaValor(c, "Model SAP Part Number (FG/PCBA/T&C si aplica)", sapPartNumber);
        DrawFilaEtiquetaValor(c, "BCS Part Number (vacío si es igual al SAP PN)", s.BcsPartNumber);
        DrawFilaEtiquetaValor(c, "Client Part Number (FG/PCBA si aplica)", clientPartNumber);
        DrawFilaEtiquetaValor(c, "Laser Serial Number real example", seriales);

        DrawTituloSeccion(c, "3. PCB / SAP Information");
        DrawFilaEtiquetaValor(c, "PCBs per Panel", s.PcbsPerPanel);
        DrawGrupoPcbSap(c, "SAP Part Number Report", s.AreasPcbSap.Select(a => (a.Area, a.SapPartNumberReport)).ToList());
        DrawGrupoPcbSap(c, "SAP Report Side (Top/Bot/N-A)", s.AreasPcbSap.Where(a => a.TieneReportSide).Select(a => (a.Area, a.SapReportSide)).ToList());
        DrawGrupoPcbSap(c, "SAP Report Point (Station)", s.AreasPcbSap.Select(a => (a.Area, a.SapReportPoint)).ToList());

        DrawTituloSeccion(c, "4. Line Information (add as many rows as you need)");
        DrawTablaLineas(c, s.Lineas);

        DrawTituloSeccion(c, "5. Provide the model line flow diagram (operation by operation)");
        DrawDiagramaFlujo(c, s.RutaImagenDiagramaFlujo);

        DrawTituloSeccion(c, "6. By each operation, provide the current MES Interfaces available");
        DrawTablaOperaciones(c, s.Operaciones);

        DrawTituloSeccion(c, "7. Packout Setup Data");
        DrawFilaEtiquetaValor(c, "Finish Good or PCB Packout?", s.PackoutTipo);
        DrawFilaEtiquetaValor(c, "Validate flow before Pack?", s.ValidarFlujoAntesDePack ? "Yes" : "No");
        DrawFilaEtiquetaValor(c, "Last operation before Packout", s.UltimaOperacionAntesDePackout);
        DrawFilaEtiquetaValor(c, "New Packout Station? (fecha si aplica)",
            s.NuevaEstacionPackout ? s.FechaInstalacionEstacion?.ToString("d/M/yyyy") ?? "Yes" : "No");
        DrawFilaEtiquetaValor(c, "Box Label QR code", s.BoxLabelQr);
        DrawFilaEtiquetaValor(c, "Product Label QR code", s.ProductLabelQr);

        DrawTituloSeccion(c, "8. Approvals (Name and date)");
        foreach (var a in s.Aprobaciones)
        {
            DrawFilaEtiquetaValor(c, a.Rol, a.Nombre);
        }

        documento.Save(filePath);
    }

    public static void ExportCambioDeFlujo(string filePath, SolicitudCambioFlujo s)
    {
        using var documento = new PdfDocument();
        documento.Info.Title = $"Flow change request — {s.NombreModelo}";
        documento.Info.Author = s.NombreSolicitante;

        var c = new Cursor { Documento = documento, Fonts = new Fonts() };
        c.NuevaPagina();

        DrawEncabezado(c, "BCS Unity System: Flow change request", "Please provide the requested information");

        DrawSeccionGeneral(c, s.FechaSolicitud, s.NombreSolicitante, s.CorreoSolicitante, s.FechaEsperada,
            "MES Team will need up to 5 working days to add a new model configuration into Unity");

        DrawTituloSeccion(c, "2. Model Information");
        DrawFilaEtiquetaValor(c, "Model Name", s.NombreModelo);
        DrawFilaEtiquetaValor(c, "PCB Model SAP Part Number", s.PcbModelSapPartNumber);
        DrawFilaEtiquetaValor(c, "FG Model SAP Part Number", s.FgModelSapPartNumber);
        DrawFilaEtiquetaValor(c, "BCS Part Number (vacío si es igual al SAP PN)", s.BcsPartNumber);
        DrawFilaEtiquetaValor(c, "Client Part Number (FG/PCBA si aplica)", s.ClientPartNumber);
        DrawFilaEtiquetaValor(c, "PCB Laser Serial Number real example", s.PcbLaserSerialNumber);
        DrawFilaEtiquetaValor(c, "FG Laser Serial Number real example", s.FgLaserSerialNumber);

        DrawTituloSeccion(c, "3. Line Information (add as many rows as you need)");
        DrawTablaLineas(c, s.Lineas);

        DrawTituloSeccion(c, "4. Provide the model line flow diagram (operation by operation)");
        DrawDiagramaFlujo(c, s.RutaImagenDiagramaFlujo);

        DrawTituloSeccion(c, "5. By each operation, provide the current MES Interfaces available (ONLY PCB OR TLA FLOW)");
        DrawTablaOperaciones(c, s.Operaciones);

        DrawTituloSeccion(c, "6. Packout Setup Data");
        DrawFilaEtiquetaValor(c, "Finish Good or PCB Packout?", s.PackoutTipo);
        DrawFilaEtiquetaValor(c, "Validate flow before Pack?", s.ValidarFlujoAntesDePack ? "Yes" : "No");
        DrawFilaEtiquetaValor(c, "Last operation before Packout", s.UltimaOperacionAntesDePackout);
        DrawFilaEtiquetaValor(c, "Box Label QR code", s.BoxLabelQr);

        DrawTituloSeccion(c, "7. Approvals (Name and date)");
        foreach (var a in s.Aprobaciones)
        {
            DrawFilaEtiquetaValor(c, a.Rol, a.Nombre);
        }

        documento.Save(filePath);
    }

    public static void ExportCambioDeModelo(string filePath, SolicitudCambioModelo s)
    {
        using var documento = new PdfDocument();
        documento.Info.Title = $"Model change request — {s.ModelName.Nuevo}";
        documento.Info.Author = s.NombreSolicitante;

        var c = new Cursor { Documento = documento, Fonts = new Fonts() };
        c.NuevaPagina();

        DrawEncabezado(c, "BCS Unity System: Model change request",
            "Please provide the requested information for any field you want to change. If a field is blank MES Team will not make any change to that field");

        DrawSeccionGeneral(c, s.FechaSolicitud, s.NombreSolicitante, s.CorreoSolicitante, s.FechaEsperada,
            "MES Team will need up to 5 working days to change and validate the requested configuration changes");

        DrawTituloSeccion(c, "2. Model Information");
        DrawEncabezadoActualNuevo(c, 190);
        DrawFilaEtiquetaActualNuevo(c, s.ModelName, 190);
        DrawFilaEtiquetaActualNuevo(c, s.ModelSapPartNumber, 190);
        DrawFilaEtiquetaActualNuevo(c, s.BcsPartNumber, 190);
        DrawFilaEtiquetaActualNuevo(c, s.ClientPartNumber, 190);
        DrawFilaEtiquetaActualNuevo(c, s.LaserSerialNumber, 190);

        DrawTituloSeccion(c, "3. PCB / SAP Information");
        DrawEncabezadoActualNuevo(c, 190);
        DrawFilaEtiquetaActualNuevo(c, s.PcbsPerPanel, 190);
        DrawGrupoPcbSapActualNuevo(c, "SAP Part Number Report", s.AreasPcbSap.Select(a => (a.Area, a.SapPartNumberReportActual, a.SapPartNumberReportNuevo)).ToList());
        DrawGrupoPcbSapActualNuevo(c, "SAP Report Side", s.AreasPcbSap.Where(a => a.TieneReportSide).Select(a => (a.Area, a.SapReportSideActual, a.SapReportSideNuevo)).ToList());
        DrawGrupoPcbSapActualNuevo(c, "SAP Report Point", s.AreasPcbSap.Select(a => (a.Area, a.SapReportPointActual, a.SapReportPointNuevo)).ToList());

        DrawTituloSeccion(c, "4. Packout Setup Data");
        DrawEncabezadoActualNuevo(c, 200, "CURRENTLY USED", "NEW TO BE ADDED");
        DrawFilaEtiquetaActualNuevo(c, s.PackoutTipo, 200);
        DrawFilaEtiquetaActualNuevo(c, s.ValidarFlujoAntesDePack, 200);
        DrawFilaEtiquetaActualNuevo(c, s.UltimaOperacionAntesDePackout, 200);
        DrawFilaEtiquetaActualNuevo(c, s.NuevaEstacionPackout, 200);
        DrawFilaEtiquetaActualNuevo(c, s.BoxLabelQr, 200);
        DrawFilaEtiquetaActualNuevo(c, s.ProductLabelQr, 200);

        DrawTituloSeccion(c, "5. Approvals (Name and date)");
        foreach (var a in s.Aprobaciones)
        {
            DrawFilaEtiquetaValor(c, a.Rol, a.Nombre);
        }

        documento.Save(filePath);
    }

    // ---- Bloques compartidos ----

    private static void DrawEncabezado(Cursor c, string titulo, string aviso)
    {
        c.Graficos.DrawString(titulo, c.Fonts.Titulo, XBrushes.Black, new XPoint(MarginLeft, c.Y));
        c.Y += 22;
        var altoAviso = c.AlturaNecesaria(aviso, c.Fonts.Pie, PageWidth - MarginLeft - MarginRight);
        c.AsegurarEspacio(altoAviso);
        c.Graficos.DrawString(aviso, c.Fonts.Pie, XBrushes.Black, new XRect(MarginLeft, c.Y, PageWidth - MarginLeft - MarginRight, altoAviso), XStringFormats.TopLeft);
        c.Y += altoAviso + 6;
    }

    private static void DrawSeccionGeneral(Cursor c, DateTimeOffset fecha, string solicitante, string correo, DateTimeOffset? fechaEsperada, string avisoImportante)
    {
        DrawTituloSeccion(c, "1. General Information");
        DrawFilaEtiquetaValor(c, "Request Date", fecha.ToString("d/M/yyyy"));
        DrawFilaEtiquetaValor(c, "Requestor Name", solicitante);
        DrawFilaEtiquetaValor(c, "Requestor Email", correo);
        DrawFilaEtiquetaValor(c, "Expected by", fechaEsperada?.ToString("d/M/yyyy") ?? "N/A");

        var texto = $"Important: {avisoImportante}";
        var alto = c.AlturaNecesaria(texto, c.Fonts.Etiqueta, PageWidth - MarginLeft - MarginRight);
        c.AsegurarEspacio(alto + 10);
        c.Y += 10;
        c.Graficos.DrawString(texto, c.Fonts.Etiqueta, XBrushes.Black, new XRect(MarginLeft, c.Y, PageWidth - MarginLeft - MarginRight, alto), XStringFormats.TopLeft);
        c.Y += alto;
    }

    private static void DrawTituloSeccion(Cursor c, string texto)
    {
        c.AsegurarEspacio(30);
        c.Y += 8;
        c.Graficos.DrawString(texto, c.Fonts.Seccion, XBrushes.Black, new XPoint(MarginLeft, c.Y));
        c.Y += 16;
    }

    // Fila de tabla con borde: etiqueta (columna angosta) + valor (resto
    // del ancho) — la altura se calcula según cuál de las dos necesite
    // más espacio, para que nunca se corte ni se encime con la siguiente.
    private static void DrawFilaEtiquetaValor(Cursor c, string etiqueta, string? valor, double anchoEtiqueta = 210)
    {
        var anchoValor = PageWidth - MarginLeft - MarginRight - anchoEtiqueta;
        var textoValor = string.IsNullOrWhiteSpace(valor) ? "N/A" : valor;
        var alto = Math.Max(c.AlturaNecesaria(etiqueta, c.Fonts.Etiqueta, anchoEtiqueta), c.AlturaNecesaria(textoValor, c.Fonts.Valor, anchoValor));

        c.AsegurarEspacio(alto);
        var y = c.Y;
        c.Graficos.DrawRectangle(XPens.Black, MarginLeft, y, anchoEtiqueta, alto);
        c.Graficos.DrawRectangle(XPens.Black, MarginLeft + anchoEtiqueta, y, anchoValor, alto);
        c.Graficos.DrawString(etiqueta, c.Fonts.Etiqueta, XBrushes.Black, new XRect(MarginLeft + RellenoCelda, y, anchoEtiqueta - (RellenoCelda * 2), alto), XStringFormats.TopLeft);
        c.Graficos.DrawString(textoValor, c.Fonts.Valor, XBrushes.Black, new XRect(MarginLeft + anchoEtiqueta + RellenoCelda, y, anchoValor - (RellenoCelda * 2), alto), XStringFormats.TopLeft);
        c.Y += alto;
    }

    // Grupo con una etiqueta que "abarca" varias sub-filas (área + valor) — imita una celda combinada de Word.
    private static void DrawGrupoPcbSap(Cursor c, string etiqueta, IReadOnlyList<(string Area, string Valor)> filas)
    {
        const double anchoEtiqueta = 165;
        const double anchoArea = 85;
        var anchoValor = PageWidth - MarginLeft - MarginRight - anchoEtiqueta - anchoArea;

        var altosFilas = filas.Select(f => c.AlturaNecesaria(string.IsNullOrWhiteSpace(f.Valor) ? "N/A" : f.Valor, c.Fonts.Valor, anchoValor)).ToList();
        var altoTotal = Math.Max(altosFilas.Sum(), c.AlturaNecesaria(etiqueta, c.Fonts.Etiqueta, anchoEtiqueta));

        c.AsegurarEspacio(altoTotal);
        var yInicio = c.Y;

        c.Graficos.DrawRectangle(XPens.Black, MarginLeft, yInicio, anchoEtiqueta, altoTotal);
        c.Graficos.DrawString(etiqueta, c.Fonts.Etiqueta, XBrushes.Black, new XRect(MarginLeft + RellenoCelda, yInicio, anchoEtiqueta - (RellenoCelda * 2), altoTotal), XStringFormats.TopLeft);

        var y = yInicio;
        for (var i = 0; i < filas.Count; i++)
        {
            var (area, valor) = filas[i];
            var altoFila = altosFilas[i];
            var textoValor = string.IsNullOrWhiteSpace(valor) ? "N/A" : valor;

            c.Graficos.DrawRectangle(XPens.Black, MarginLeft + anchoEtiqueta, y, anchoArea, altoFila);
            c.Graficos.DrawRectangle(XPens.Black, MarginLeft + anchoEtiqueta + anchoArea, y, anchoValor, altoFila);
            c.Graficos.DrawString(area, c.Fonts.Etiqueta, XBrushes.Black, new XRect(MarginLeft + anchoEtiqueta + RellenoCelda, y, anchoArea - (RellenoCelda * 2), altoFila), XStringFormats.TopLeft);
            c.Graficos.DrawString(textoValor, c.Fonts.Valor, XBrushes.Black, new XRect(MarginLeft + anchoEtiqueta + anchoArea + RellenoCelda, y, anchoValor - (RellenoCelda * 2), altoFila), XStringFormats.TopLeft);
            y += altoFila;
        }

        c.Y += altoTotal;
    }

    private static void DrawEncabezadoActualNuevo(Cursor c, double anchoEtiqueta, string tituloActual = "CURRENTLY", string tituloNuevo = "NEW")
    {
        var anchoColumna = (PageWidth - MarginLeft - MarginRight - anchoEtiqueta) / 2;
        const double alto = FilaAltoMinimo;
        c.AsegurarEspacio(alto);
        var y = c.Y;
        c.Graficos.DrawRectangle(XPens.Black, MarginLeft, y, anchoEtiqueta, alto);
        c.Graficos.DrawRectangle(XPens.Black, MarginLeft + anchoEtiqueta, y, anchoColumna, alto);
        c.Graficos.DrawRectangle(XPens.Black, MarginLeft + anchoEtiqueta + anchoColumna, y, anchoColumna, alto);
        c.Graficos.DrawString(tituloActual, c.Fonts.Etiqueta, XBrushes.Black, new XRect(MarginLeft + anchoEtiqueta + RellenoCelda, y, anchoColumna - (RellenoCelda * 2), alto), XStringFormats.CenterLeft);
        c.Graficos.DrawString(tituloNuevo, c.Fonts.Etiqueta, XBrushes.Black, new XRect(MarginLeft + anchoEtiqueta + anchoColumna + RellenoCelda, y, anchoColumna - (RellenoCelda * 2), alto), XStringFormats.CenterLeft);
        c.Y += alto;
    }

    private static void DrawFilaEtiquetaActualNuevo(Cursor c, CampoActualNuevo campo, double anchoEtiqueta)
    {
        var anchoColumna = (PageWidth - MarginLeft - MarginRight - anchoEtiqueta) / 2;
        var textoActual = string.IsNullOrWhiteSpace(campo.Actual) ? "N/A" : campo.Actual;
        var textoNuevo = string.IsNullOrWhiteSpace(campo.Nuevo) ? "N/A" : campo.Nuevo;
        var alto = Math.Max(
            c.AlturaNecesaria(campo.Etiqueta, c.Fonts.Etiqueta, anchoEtiqueta),
            Math.Max(c.AlturaNecesaria(textoActual, c.Fonts.Valor, anchoColumna), c.AlturaNecesaria(textoNuevo, c.Fonts.Valor, anchoColumna)));

        c.AsegurarEspacio(alto);
        var y = c.Y;
        c.Graficos.DrawRectangle(XPens.Black, MarginLeft, y, anchoEtiqueta, alto);
        c.Graficos.DrawRectangle(XPens.Black, MarginLeft + anchoEtiqueta, y, anchoColumna, alto);
        c.Graficos.DrawRectangle(XPens.Black, MarginLeft + anchoEtiqueta + anchoColumna, y, anchoColumna, alto);
        c.Graficos.DrawString(campo.Etiqueta, c.Fonts.Etiqueta, XBrushes.Black, new XRect(MarginLeft + RellenoCelda, y, anchoEtiqueta - (RellenoCelda * 2), alto), XStringFormats.TopLeft);
        c.Graficos.DrawString(textoActual, c.Fonts.Valor, XBrushes.Black, new XRect(MarginLeft + anchoEtiqueta + RellenoCelda, y, anchoColumna - (RellenoCelda * 2), alto), XStringFormats.TopLeft);
        c.Graficos.DrawString(textoNuevo, c.Fonts.Valor, XBrushes.Black, new XRect(MarginLeft + anchoEtiqueta + anchoColumna + RellenoCelda, y, anchoColumna - (RellenoCelda * 2), alto), XStringFormats.TopLeft);
        c.Y += alto;
    }

    private static void DrawGrupoPcbSapActualNuevo(Cursor c, string etiqueta, IReadOnlyList<(string Area, string Actual, string Nuevo)> filas)
    {
        const double anchoEtiqueta = 135;
        const double anchoArea = 65;
        var anchoColumna = (PageWidth - MarginLeft - MarginRight - anchoEtiqueta - anchoArea) / 2;

        var altosFilas = filas.Select(f =>
        {
            var textoActual = string.IsNullOrWhiteSpace(f.Actual) ? "N/A" : f.Actual;
            var textoNuevo = string.IsNullOrWhiteSpace(f.Nuevo) ? "N/A" : f.Nuevo;
            return Math.Max(c.AlturaNecesaria(textoActual, c.Fonts.Valor, anchoColumna), c.AlturaNecesaria(textoNuevo, c.Fonts.Valor, anchoColumna));
        }).ToList();
        var altoTotal = Math.Max(altosFilas.Sum(), c.AlturaNecesaria(etiqueta, c.Fonts.Etiqueta, anchoEtiqueta));

        c.AsegurarEspacio(altoTotal);
        var yInicio = c.Y;

        c.Graficos.DrawRectangle(XPens.Black, MarginLeft, yInicio, anchoEtiqueta, altoTotal);
        c.Graficos.DrawString(etiqueta, c.Fonts.Etiqueta, XBrushes.Black, new XRect(MarginLeft + RellenoCelda, yInicio, anchoEtiqueta - (RellenoCelda * 2), altoTotal), XStringFormats.TopLeft);

        var y = yInicio;
        for (var i = 0; i < filas.Count; i++)
        {
            var (area, actual, nuevo) = filas[i];
            var altoFila = altosFilas[i];
            var textoActual = string.IsNullOrWhiteSpace(actual) ? "N/A" : actual;
            var textoNuevo = string.IsNullOrWhiteSpace(nuevo) ? "N/A" : nuevo;

            var x = MarginLeft + anchoEtiqueta;
            c.Graficos.DrawRectangle(XPens.Black, x, y, anchoArea, altoFila);
            c.Graficos.DrawString(area, c.Fonts.Etiqueta, XBrushes.Black, new XRect(x + RellenoCelda, y, anchoArea - (RellenoCelda * 2), altoFila), XStringFormats.TopLeft);
            x += anchoArea;
            c.Graficos.DrawRectangle(XPens.Black, x, y, anchoColumna, altoFila);
            c.Graficos.DrawString(textoActual, c.Fonts.Valor, XBrushes.Black, new XRect(x + RellenoCelda, y, anchoColumna - (RellenoCelda * 2), altoFila), XStringFormats.TopLeft);
            x += anchoColumna;
            c.Graficos.DrawRectangle(XPens.Black, x, y, anchoColumna, altoFila);
            c.Graficos.DrawString(textoNuevo, c.Fonts.Valor, XBrushes.Black, new XRect(x + RellenoCelda, y, anchoColumna - (RellenoCelda * 2), altoFila), XStringFormats.TopLeft);
            y += altoFila;
        }

        c.Y += altoTotal;
    }

    private static void DrawTablaLineas(Cursor c, IEnumerable<LineaInfo> lineas)
    {
        var anchos = new double[] { 95, 70, 70, 65, 75, 90 };
        DrawFilaTabla(c, anchos, new[] { "Line Name", "PCB Side", "Line Efficiency", "Cycle Time", "Full Rate / hr", "Expected Rate / hr" }, negrita: true);
        foreach (var l in lineas)
        {
            DrawFilaTabla(c, anchos, new[] { l.Nombre, l.PcbSide, l.Eficiencia, l.CycleTime, l.FullRate, l.ExpectedRate });
        }
    }

    private static void DrawTablaOperaciones(Cursor c, IEnumerable<OperacionInfo> operaciones)
    {
        var anchos = new double[] { 125, 80, 105, 85, 100 };
        DrawFilaTabla(c, anchos, new[] { "Operation Name", "Line Name", "MES Interface", "Interlock Flow", "Physical Interlock" }, negrita: true);
        foreach (var o in operaciones)
        {
            DrawFilaTabla(c, anchos, new[]
            {
                string.IsNullOrWhiteSpace(o.Numero) ? o.Nombre : $"{o.Numero} {o.Nombre}",
                o.LineaAsociada,
                o.MesInterface,
                o.RequeridaParaInterlock ? "Yes" : "No",
                o.TieneInterlockFisico ? "Yes" : "No"
            });
        }
    }

    private static void DrawFilaTabla(Cursor c, double[] anchos, string?[] textos, bool negrita = false)
    {
        var fuente = negrita ? c.Fonts.Etiqueta : c.Fonts.Valor;
        var textosFinales = new string[anchos.Length];
        for (var i = 0; i < anchos.Length; i++)
        {
            var texto = i < textos.Length ? textos[i] : null;
            textosFinales[i] = string.IsNullOrWhiteSpace(texto) ? (negrita ? string.Empty : "N/A") : texto!;
        }

        var alto = FilaAltoMinimo;
        for (var i = 0; i < anchos.Length; i++)
        {
            alto = Math.Max(alto, c.AlturaNecesaria(textosFinales[i], fuente, anchos[i]));
        }

        c.AsegurarEspacio(alto);
        var x = MarginLeft;
        var y = c.Y;

        for (var i = 0; i < anchos.Length; i++)
        {
            c.Graficos.DrawRectangle(XPens.Black, x, y, anchos[i], alto);
            c.Graficos.DrawString(textosFinales[i], fuente, XBrushes.Black, new XRect(x + RellenoCelda, y, anchos[i] - (RellenoCelda * 2), alto), XStringFormats.TopLeft);
            x += anchos[i];
        }

        c.Y += alto;
    }

    private static void DrawDiagramaFlujo(Cursor c, string? rutaImagen)
    {
        if (string.IsNullOrWhiteSpace(rutaImagen) || !File.Exists(rutaImagen))
        {
            c.AsegurarEspacio(FilaAltoMinimo);
            c.Graficos.DrawString("(No se adjuntó una imagen del diagrama de flujo)", c.Fonts.Valor, XBrushes.Gray, new XPoint(MarginLeft, c.Y));
            c.Y += FilaAltoMinimo;
            return;
        }

        try
        {
            using var imagen = XImage.FromFile(rutaImagen);
            var anchoDisponible = PageWidth - MarginLeft - MarginRight;
            const double altoMaximo = 320;

            var ancho = anchoDisponible;
            var alto = ancho * imagen.PixelHeight / imagen.PixelWidth;
            if (alto > altoMaximo)
            {
                var factor = altoMaximo / alto;
                alto = altoMaximo;
                ancho *= factor;
            }

            c.AsegurarEspacio(alto + 10);
            c.Graficos.DrawImage(imagen, MarginLeft, c.Y, ancho, alto);
            c.Y += alto + 10;
        }
        catch (Exception)
        {
            c.AsegurarEspacio(FilaAltoMinimo);
            c.Graficos.DrawString("(No se pudo cargar la imagen del diagrama)", c.Fonts.Valor, XBrushes.Gray, new XPoint(MarginLeft, c.Y));
            c.Y += FilaAltoMinimo;
        }
    }

    private static string JuntarConEtiqueta(params (string Etiqueta, string Valor)[] partes)
    {
        var conValor = partes.Where(p => !string.IsNullOrWhiteSpace(p.Valor)).Select(p => $"{p.Etiqueta}: {p.Valor}");
        return string.Join("  ", conValor);
    }
}
