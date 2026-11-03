using Alejandria.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Alejandria.Services;

// Reporte PDF: portada BCS y una página por área
public static class PdfReportService
{
    private const double PageWidthPt = 595; // A4 a 72pt por pulgada
    private const double PageHeightPt = 842;
    private const double MarginLeft = 40;
    private const double LabelColumnWidth = 260; // Cabe MODELS_SERIALNUMBERSPECS Packing
    private const double LineHeight = 22;
    private const string AccentHex = StatusColorHelper.AccentHex;

    // Alto de la franja de arriba (fecha + logo)
    private const double CompanyStripHeight = 34;

    // alcanceDescripcion null = reporte general
    public static void Export(
        string filePath,
        IReadOnlyList<ModelRecord> modelos,
        IReadOnlyDictionary<int, List<DetalleConfiguracion>> detallesPorModelo,
        string? alcanceDescripcion = null)
    {
        using var document = new PdfDocument();
        document.Info.Title = "Alejandría — Reporte de configuración";
        document.Info.Author = "Alejandría";
        document.Info.Subject = "BCS Automotive Interface Solutions Querétaro — antecedente de configuración de modelo";

        var fonts = new ReportFonts();
        var appLogo = TryLoadImage("AppLogo.png");
        var bcsLogo = TryLoadImage("BcsLogo.jpg");

        DrawCoverPage(document, fonts, bcsLogo, modelos, detallesPorModelo, alcanceDescripcion);

        if (modelos.Count == 0)
        {
            var page = document.AddPage();
            SetA4(page);
            var gfx = XGraphics.FromPdfPage(page);
            DrawCompanyStrip(gfx, fonts, bcsLogo);
            DrawHeader(gfx, fonts, appLogo, alcanceDescripcion);
            gfx.DrawString("No hay modelos en Histórico todavía.", fonts.Title, XBrushes.Black, new XPoint(MarginLeft, 140 + CompanyStripHeight));
            DrawFooter(gfx, fonts, document.PageCount);
        }

        foreach (var modelo in modelos)
        {
            detallesPorModelo.TryGetValue(modelo.Id, out var detallesDelModelo);
            var detallesPorArea = (detallesDelModelo ?? new List<DetalleConfiguracion>()).ToDictionary(d => d.Area, d => d);

            // Estatus de todo el modelo en cada página
            var resumenModelo = ConfigStatusHelper.ComputeResumenModelo(modelo, detallesDelModelo);
            var areas = modelo.AreasDisponibles;

            // Si un área tiene el documento pendiente, el aviso sale en todas las páginas del modelo
            var modeloTienePendiente = ConfigStatusHelper.TieneDocumentoPendienteCorreccion(detallesDelModelo);

            // Una página por área
            for (var i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                detallesPorArea.TryGetValue(area, out var detalle);
                detalle ??= new DetalleConfiguracion { ModeloId = modelo.Id, Area = area };

                var resumenArea = ConfigStatusHelper.ComputeResumen(modelo, detalle);

                var page = document.AddPage();
                SetA4(page);
                var gfx = XGraphics.FromPdfPage(page);

                if (modeloTienePendiente)
                {
                    DrawMarcaDeAguaPendiente(gfx);
                }

                DrawCompanyStrip(gfx, fonts, bcsLogo);
                DrawHeader(gfx, fonts, appLogo, alcanceDescripcion);

                double y = 110 + CompanyStripHeight;

                var etiquetaArea = areas.Count > 1 ? $"  —  Área: {area}  ({i + 1} de {areas.Count})" : string.Empty;
                gfx.DrawString($"{modelo.NumeroModelo} — {modelo.NombreModelo}{etiquetaArea}", fonts.Title, XBrushes.Black, new XPoint(MarginLeft, y));
                y += 22;

                var numeroParteArea = modelo.GetNumeroParte(area);
                var lineaFecha = string.IsNullOrWhiteSpace(numeroParteArea)
                    ? $"Fecha de solicitud: {modelo.FechaSolicitud:dd/MM/yyyy}"
                    : $"Número de parte ({area}): {numeroParteArea}   ·   Fecha de solicitud: {modelo.FechaSolicitud:dd/MM/yyyy}";
                gfx.DrawString(lineaFecha, fonts.Value, XBrushes.Black, new XPoint(MarginLeft, y));
                y += LineHeight - 4;

                if (areas.Count > 1)
                {
                    gfx.DrawString($"Todas las áreas de este modelo: {modelo.AreasAplicablesTexto}", fonts.Footer, XBrushes.Gray, new XPoint(MarginLeft, y));
                    y += LineHeight - 8;
                }
                y += 10;

                gfx.DrawString("Estatus de esta área:", fonts.Label, XBrushes.Black, new XPoint(MarginLeft, y));
                DrawBadge(gfx, resumenArea.EstatusGeneral, MarginLeft + 140, y - 12, fonts.Badge);
                y += 26;

                if (areas.Count > 1)
                {
                    gfx.DrawString("Estatus del modelo completo:", fonts.Label, XBrushes.Black, new XPoint(MarginLeft, y));
                    DrawBadge(gfx, resumenModelo.EstatusGeneral, MarginLeft + 190, y - 12, fonts.Badge);
                    y += 26;
                }
                y += 8;

                if (modeloTienePendiente)
                {
                    y = DrawAvisoDocumentoPendiente(gfx, fonts, y);
                }

                y = WriteSection(gfx, fonts, "Configuración Base de Datos — iMES", y,
                    ("MODELS_PARENTS", detalle.ImesModelsParents, true),
                    ("MODELS_MASTER", detalle.ImesModelsMaster, true),
                    ("MODELS_SERIALNUMBERSPECS", detalle.ImesModelsSerialNumberSpecs, true),
                    ("MODELS_SERIALNUMBERSPECS Packing", detalle.ImesModelsSerialNumberSpecsPacking, true));

                WriteSection(gfx, fonts, "Configuración Plataforma iMES", y,
                    ("Work Order", detalle.WorkOrder, false),
                    ConfigStatusHelper.EsAreaSmt(area) ? ("Ruta", detalle.Ruta, false) : ("Flujo", detalle.Flujo, false),
                    ("Línea", detalle.Linea, false),
                    ("PartLineBomMaintenance", detalle.PartLineBomMaintenance, true));

                DrawFooter(gfx, fonts, document.PageCount);
            }
        }

        document.Save(filePath);
    }

    private sealed class ReportFonts
    {
        public XFont HeaderTitle { get; } = new("Segoe UI", 15, XFontStyleEx.Bold);
        public XFont HeaderSubtitle { get; } = new("Segoe UI", 9, XFontStyleEx.Regular);
        public XFont Title { get; } = new("Segoe UI", 15, XFontStyleEx.Bold);
        public XFont Section { get; } = new("Segoe UI", 11, XFontStyleEx.Bold);
        public XFont Label { get; } = new("Segoe UI", 10, XFontStyleEx.Bold);
        public XFont Value { get; } = new("Segoe UI", 10, XFontStyleEx.Regular);
        public XFont Badge { get; } = new("Segoe UI", 9, XFontStyleEx.Bold);
        public XFont Footer { get; } = new("Segoe UI", 8, XFontStyleEx.Regular);

        // Fuentes de la portada
        public XFont StripText { get; } = new("Segoe UI", 7, XFontStyleEx.Regular);
        public XFont CompanyName { get; } = new("Segoe UI", 12, XFontStyleEx.Bold);
        public XFont CoverTitle { get; } = new("Segoe UI", 19, XFontStyleEx.Bold);
        public XFont CoverSubtitle { get; } = new("Segoe UI", 14, XFontStyleEx.Bold);
        public XFont CoverLabel { get; } = new("Segoe UI", 9, XFontStyleEx.Bold);
        public XFont CoverValue { get; } = new("Segoe UI", 9.5, XFontStyleEx.Regular);
    }

    // Portada BCS: fecha, logo, título, dirección, autores y datos del reporte
    private static void DrawCoverPage(PdfDocument document, ReportFonts fonts, XImage? bcsLogo, IReadOnlyList<ModelRecord> modelos, IReadOnlyDictionary<int, List<DetalleConfiguracion>> detallesPorModelo, string? alcanceDescripcion)
    {
        var page = document.AddPage();
        SetA4(page);
        var gfx = XGraphics.FromPdfPage(page);

        // Si algún modelo tiene el documento pendiente, el aviso sale también en la portada
        var algunoTienePendiente = modelos.Any(m =>
        {
            detallesPorModelo.TryGetValue(m.Id, out var detalles);
            return ConfigStatusHelper.TieneDocumentoPendienteCorreccion(detalles);
        });

        if (algunoTienePendiente)
        {
            DrawMarcaDeAguaPendiente(gfx);
        }

        var pen = new XPen(ParseColor("#1E293B"), 1);
        double left = MarginLeft;
        double right = PageWidthPt - MarginLeft;
        double top = 40;

        // Encabezado: fecha, empresa y logo
        const double headerHeight = 70;
        gfx.DrawRectangle(pen, new XRect(left, top, right - left, headerHeight));

        double col1 = left + 150;
        double col2 = right - 130;
        gfx.DrawLine(pen, col1, top, col1, top + headerHeight);
        gfx.DrawLine(pen, col2, top, col2, top + headerHeight);

        gfx.DrawString(
            $"FECHA: {DateTime.Now:dd MMM yyyy}",
            fonts.StripText,
            XBrushes.Black,
            new XRect(left, top, col1 - left, headerHeight),
            XStringFormats.Center);

        gfx.DrawString(
            "BCS Automotive Interface Solutions",
            fonts.CompanyName,
            XBrushes.Black,
            new XRect(col1, top, col2 - col1, headerHeight),
            XStringFormats.Center);

        if (bcsLogo is not null)
        {
            var logoWidth = Math.Min(110, right - col2 - 10);
            var logoHeight = logoWidth * bcsLogo.PixelHeight / bcsLogo.PixelWidth;
            gfx.DrawImage(bcsLogo, col2 + (right - col2 - logoWidth) / 2, top + (headerHeight - logoHeight) / 2, logoWidth, logoHeight);
        }

        double y = top + headerHeight + 24;

        // Título
        gfx.DrawString(
            "BCS Automotive Interface Solutions Querétaro",
            fonts.CoverTitle,
            XBrushes.Black,
            new XRect(left, y, right - left, 50),
            XStringFormats.TopCenter);
        y += 55;

        var subtitleRect = new XRect(left, y, right - left, 30);
        gfx.DrawRectangle(pen, subtitleRect);
        gfx.DrawString("Reporte de Configuración de Modelo", fonts.CoverSubtitle, XBrushes.Black, subtitleRect, XStringFormats.Center);
        y += 46;

        // Dirección
        gfx.DrawString("Dirección:", fonts.CoverValue, XBrushes.Black, new XPoint(left, y));
        y += 15;
        foreach (var linea in new[]
                 {
                     "BCS Automotive Interface Solutions Querétaro",
                     "Carretera Estatal 431",
                     "76246 Querétaro, El Marques",
                     "México"
                 })
        {
            gfx.DrawString(linea, fonts.CoverValue, XBrushes.Black, new XPoint(left, y));
            y += 14;
        }
        y += 18;

        // Autores, owner y aprobador
        y = DrawCoverRow(gfx, fonts, pen, left, right, y, "Autores:", "Alejandría — App de Seguimiento de Altas de Modelos");
        y = DrawCoverRow(gfx, fonts, pen, left, right, y, "Owner:", "Departamento de MES");
        y = DrawCoverRow(gfx, fonts, pen, left, right, y, "Aprobador:", "Reynaldo Barrera — MES Team Lead");
        y += 22;

        // Datos del reporte
        gfx.DrawString("Datos de este reporte", fonts.CoverSubtitle, new XSolidBrush(ParseColor(AccentHex)), new XPoint(left, y));
        y += 20;

        var alcanceTexto = string.IsNullOrEmpty(alcanceDescripcion)
            ? "General — todos los modelos en Histórico (Estatus General Terminado)"
            : alcanceDescripcion;

        y = DrawInfoLine(gfx, fonts, left, right, y, "Alcance del reporte:", alcanceTexto);
        y = DrawInfoLine(gfx, fonts, left, right, y, "Modelos incluidos:", modelos.Count.ToString());
        y = DrawInfoLine(gfx, fonts, left, right, y, "Fecha de generación:", $"{DateTime.Now:dd/MM/yyyy HH:mm}");
        y = DrawInfoLine(gfx, fonts, left, right, y, "Propósito:", "Antecedente de que la configuración de BD (iMES) y Plataforma iMES ya se realizó.");

        if (algunoTienePendiente)
        {
            y += 10;
            DrawAvisoDocumentoPendiente(gfx, fonts, y);
        }
    }

    // Fila de autores/owner/aprobador
    private static double DrawCoverRow(XGraphics gfx, ReportFonts fonts, XPen pen, double left, double right, double y, string label, string value)
    {
        const double rowHeight = 34;
        var rect = new XRect(left, y, right - left, rowHeight);
        gfx.DrawRectangle(pen, rect);
        gfx.DrawString(label, fonts.CoverLabel, XBrushes.Black, new XPoint(left + 8, y + 14));
        gfx.DrawString(value, fonts.CoverValue, XBrushes.Black, new XPoint(left + 8, y + 27));
        return y + rowHeight;
    }

    // "Etiqueta: valor" con salto de línea
    private static double DrawInfoLine(XGraphics gfx, ReportFonts fonts, double left, double right, double y, string label, string value)
    {
        gfx.DrawString(label, fonts.CoverLabel, XBrushes.Black, new XPoint(left, y));

        var lineas = WrapText(gfx, value, fonts.CoverValue, right - left);
        var yValor = y + 13;
        foreach (var linea in lineas)
        {
            gfx.DrawString(linea, fonts.CoverValue, XBrushes.Black, new XPoint(left, yValor));
            yValor += 13;
        }

        return yValor + 8;
    }

    // Franja de arriba en las páginas de detalle
    private static void DrawCompanyStrip(XGraphics gfx, ReportFonts fonts, XImage? bcsLogo)
    {
        var pen = new XPen(ParseColor("#CBD5E1"), 0.75);
        gfx.DrawLine(pen, 0, CompanyStripHeight, PageWidthPt, CompanyStripHeight);

        gfx.DrawString(
            $"FECHA: {DateTime.Now:dd/MM/yyyy}",
            fonts.StripText,
            XBrushes.Black,
            new XPoint(MarginLeft, CompanyStripHeight - 12));

        if (bcsLogo is not null)
        {
            var logoWidth = 78.0;
            var logoHeight = logoWidth * bcsLogo.PixelHeight / bcsLogo.PixelWidth;
            gfx.DrawImage(bcsLogo, PageWidthPt - MarginLeft - logoWidth, (CompanyStripHeight - logoHeight) / 2, logoWidth, logoHeight);
        }
    }

    // Banda azul con logo y alcance
    private static void DrawHeader(XGraphics gfx, ReportFonts fonts, XImage? logo, string? alcanceDescripcion)
    {
        var bannerBrush = new XSolidBrush(ParseColor(AccentHex));
        gfx.DrawRectangle(bannerBrush, new XRect(0, CompanyStripHeight, PageWidthPt, 70));

        double textX = MarginLeft;
        if (logo is not null)
        {
            gfx.DrawImage(logo, MarginLeft, CompanyStripHeight + 15, 40, 40);
            textX = MarginLeft + 52;
        }

        gfx.DrawString("Alejandría — Reporte de configuración", fonts.HeaderTitle, XBrushes.White, new XPoint(textX, CompanyStripHeight + 32));

        var subtitulo = string.IsNullOrEmpty(alcanceDescripcion)
            ? $"Reporte general (Histórico) · Generado el {DateTime.Now:dd/MM/yyyy HH:mm}"
            : $"Reporte especializado — {alcanceDescripcion} · Generado el {DateTime.Now:dd/MM/yyyy HH:mm}";
        gfx.DrawString(subtitulo, fonts.HeaderSubtitle, XBrushes.White, new XPoint(textX, CompanyStripHeight + 50));
    }

    private static void DrawFooter(XGraphics gfx, ReportFonts fonts, int pageNumber)
    {
        var lineBrush = new XSolidBrush(ParseColor("#E2E8F0"));
        gfx.DrawRectangle(lineBrush, new XRect(MarginLeft, PageHeightPt - 40, PageWidthPt - MarginLeft * 2, 0.75));
        gfx.DrawString("Alejandría · Seguimiento de Altas de Modelos", fonts.Footer, XBrushes.Gray, new XPoint(MarginLeft, PageHeightPt - 26));
        gfx.DrawString(pageNumber.ToString(), fonts.Footer, XBrushes.Gray, new XRect(0, PageHeightPt - 26, PageWidthPt - MarginLeft, 12), XStringFormats.TopRight);
    }

    // campos: (etiqueta, valor, esEstatus); esEstatus dibuja badge
    private static double WriteSection(XGraphics gfx, ReportFonts fonts, string title, double y, params (string Label, string Value, bool EsEstatus)[] campos)
    {
        var sectionBg = new XSolidBrush(ParseColor("#EFF6FF"));
        gfx.DrawRectangle(sectionBg, new XRect(MarginLeft, y, PageWidthPt - MarginLeft * 2, 20));
        gfx.DrawString(title, fonts.Section, new XSolidBrush(ParseColor("#1D4ED8")), new XPoint(MarginLeft + 8, y + 15));
        y += 30;

        foreach (var (label, value, esEstatus) in campos)
        {
            gfx.DrawString(label, fonts.Label, XBrushes.Black, new XPoint(MarginLeft + 10, y + 10));

            if (esEstatus)
            {
                DrawBadge(gfx, value, MarginLeft + LabelColumnWidth, y, fonts.Badge);
            }
            else
            {
                var texto = string.IsNullOrWhiteSpace(value) ? "(sin capturar)" : value;
                gfx.DrawString(texto, fonts.Value, XBrushes.Black, new XPoint(MarginLeft + LabelColumnWidth, y + 10));
            }

            y += LineHeight;
        }

        return y + 14;
    }

    // Un pincel por color, se reutiliza
    private static readonly Dictionary<string, XSolidBrush> BrushesPorHex = new();

    private static XSolidBrush ObtenerBrush(string hex)
    {
        if (!BrushesPorHex.TryGetValue(hex, out var brush))
        {
            brush = new XSolidBrush(ParseColor(hex));
            BrushesPorHex[hex] = brush;
        }

        return brush;
    }

    // Pastilla con el valor o Pendiente. En el PDF no sale quién lo hizo, eso está en Ver detalles
    private static void DrawBadge(XGraphics gfx, string value, double x, double y, XFont font)
    {
        var valorReal = string.IsNullOrWhiteSpace(value) ? ConfigStatusHelper.Pendiente : value;
        var hex = StatusColorHelper.GetHex(valorReal);
        var brush = ObtenerBrush(hex);

        var texto = Array.IndexOf(ConfigStatusHelper.Responsables, valorReal) >= 0
            ? ConfigStatusHelper.Terminado
            : valorReal;

        var size = gfx.MeasureString(texto, font);
        const double paddingX = 8;
        const double paddingY = 3;
        var rect = new XRect(x, y, size.Width + paddingX * 2, size.Height + paddingY * 2);

        gfx.DrawRectangle(brush, rect);
        gfx.DrawString(texto, font, XBrushes.White, rect, XStringFormats.Center);
    }

    private static readonly XFont FontMarcaDeAgua = new("Segoe UI", 40, XFontStyleEx.Bold);

    // Marca de agua, se dibuja primero para que quede atrás
    private static void DrawMarcaDeAguaPendiente(XGraphics gfx)
    {
        var state = gfx.Save();

        var brush = new XSolidBrush(XColor.FromArgb(45, 220, 38, 38));

        gfx.TranslateTransform(PageWidthPt / 2, PageHeightPt / 2);
        gfx.RotateTransform(-35);
        gfx.DrawString("PENDIENTE DE CORRECCIÓN", FontMarcaDeAgua, brush, new XRect(-320, -25, 640, 50), XStringFormats.Center);

        gfx.Restore(state);
    }

    // Aviso de documento pendiente con el plazo de 24 h.
    // DrawString con XRect no hace wrap, se parte el texto a mano
    private static double DrawAvisoDocumentoPendiente(XGraphics gfx, ReportFonts fonts, double y)
    {
        const string texto = "DOCUMENTO PENDIENTE DE CORRECCIÓN — Si no se recibe el documento corregido dentro de las próximas 24 horas, esta configuración se inhabilita.";
        var ancho = PageWidthPt - MarginLeft * 2;
        const double relleno = 12;

        var lineas = WrapText(gfx, texto, fonts.Label, ancho - relleno * 2);
        var altoLinea = fonts.Label.Height + 4;
        var alto = (lineas.Count * altoLinea) + (relleno * 2);

        var fondo = new XSolidBrush(ParseColor("#FEE2E2"));
        var borde = new XPen(ParseColor("#EF4444"), 1.5);
        var texturaTexto = new XSolidBrush(ParseColor("#991B1B"));

        var rect = new XRect(MarginLeft, y, ancho, alto);
        gfx.DrawRectangle(fondo, rect);
        gfx.DrawRectangle(borde, rect);

        var lineaY = y + relleno;
        foreach (var linea in lineas)
        {
            gfx.DrawString(linea, fonts.Label, texturaTexto, new XRect(MarginLeft, lineaY, ancho, altoLinea), XStringFormats.TopCenter);
            lineaY += altoLinea;
        }

        return y + alto + 14;
    }

    // Parte el texto en líneas que quepan
    private static List<string> WrapText(XGraphics gfx, string texto, XFont fuente, double anchoMaximo)
    {
        var palabras = texto.Split(' ');
        var lineas = new List<string>();
        var lineaActual = string.Empty;

        foreach (var palabra in palabras)
        {
            var prueba = string.IsNullOrEmpty(lineaActual) ? palabra : $"{lineaActual} {palabra}";
            if (gfx.MeasureString(prueba, fuente).Width > anchoMaximo && !string.IsNullOrEmpty(lineaActual))
            {
                lineas.Add(lineaActual);
                lineaActual = palabra;
            }
            else
            {
                lineaActual = prueba;
            }
        }

        if (!string.IsNullOrEmpty(lineaActual))
        {
            lineas.Add(lineaActual);
        }

        return lineas;
    }

    private static XImage? TryLoadImage(string fileName)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
            return File.Exists(path) ? XImage.FromFile(path) : null;
        }
        catch (Exception)
        {
            // Las imágenes son decorativas
            return null;
        }
    }

    private static XColor ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        var r = System.Convert.ToInt32(hex.Substring(0, 2), 16);
        var g = System.Convert.ToInt32(hex.Substring(2, 2), 16);
        var b = System.Convert.ToInt32(hex.Substring(4, 2), 16);
        return XColor.FromArgb(255, r, g, b);
    }

    private static void SetA4(PdfPage page)
    {
        page.Width = PageWidthPt;
        page.Height = PageHeightPt;
    }
}
