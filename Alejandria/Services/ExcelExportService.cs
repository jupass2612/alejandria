using ClosedXML.Excel;
using Alejandria.Models;
using Alejandria.ViewModels;

namespace Alejandria.Services;

// Genera el libro de Excel. Se corre en un hilo de fondo porque escribir el .xlsx no es instantáneo.
public static class ExcelExportService
{
    private const int AltasColumnCount = 9;
    private const int ModelosColumnCount = 19;

    // alcanceDescripcion: null = reporte general, si no describe el alcance especializado.
    public static void Export(
        string filePath,
        IReadOnlyList<ModelRecord> altas,
        IReadOnlyList<ModeloResumenItem> modelos,
        IReadOnlyDictionary<int, List<DetalleConfiguracion>> detallesPorModelo,
        string? alcanceDescripcion = null)
    {
        using var workbook = new XLWorkbook();

        // La portada va primero (queda como la pestaña más a la izquierda) —
        // es informativa, al estilo de la plantilla de BCS. Las hojas de
        // datos, en cambio, son las que el destinatario va a filtrar/ordenar/
        // copiar, así que llevan AutoFilter y no se tocan de otra forma.
        BuildPortadaSheet(workbook, altas.Count, alcanceDescripcion);
        BuildAltasSheet(workbook, altas, alcanceDescripcion);
        BuildModelosSheet(workbook, modelos, detallesPorModelo, alcanceDescripcion);

        workbook.SaveAs(filePath);
    }

    // Hoja de portada, misma plantilla que la del PDF. Es la única hoja
    // informativa — las de datos se quedan limpias para poder filtrarlas.
    private static void BuildPortadaSheet(XLWorkbook workbook, int totalModelos, string? filtro)
    {
        var sheet = workbook.Worksheets.Add("Portada");
        const int cols = 6;
        var lastCol = ColumnLetter(cols);

        sheet.Range($"A1:{lastCol}3").Merge();
        var banner = sheet.Cell("A1");
        banner.Value = "BCS Automotive Interface Solutions Querétaro";
        banner.Style.Fill.BackgroundColor = XLColor.FromHtml(StatusColorHelper.AccentHex);
        banner.Style.Font.FontColor = XLColor.White;
        banner.Style.Font.Bold = true;
        banner.Style.Font.FontSize = 16;
        banner.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        banner.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        banner.Style.Alignment.Indent = 2;
        sheet.Row(1).Height = 30;

        TryEmbedImage(sheet, "BcsLogo.jpg", "A1", 90, 22);

        sheet.Range($"A4:{lastCol}4").Merge();
        var subtitulo = sheet.Cell("A4");
        subtitulo.Value = "Reporte de Configuración de Modelo — antecedente de que ya se configuró";
        subtitulo.Style.Font.Italic = true;
        subtitulo.Style.Font.FontColor = XLColor.FromHtml("#64748B");

        var row = 6;
        sheet.Cell(row, 1).Value = "FECHA:";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 2).Value = DateTime.Now.Date;
        sheet.Cell(row, 2).Style.DateFormat.Format = "dd/mm/yyyy";
        row += 2;

        sheet.Cell(row, 1).Value = "Dirección:";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        row++;
        foreach (var linea in new[]
                 {
                     "BCS Automotive Interface Solutions Querétaro",
                     "Carretera Estatal 431",
                     "76246 Querétaro, El Marques",
                     "México"
                 })
        {
            sheet.Cell(row, 1).Value = linea;
            row++;
        }
        row++;

        row = WritePortadaRow(sheet, row, "Autores:", "Alejandría — App de Seguimiento de Altas de Modelos");
        row = WritePortadaRow(sheet, row, "Owner:", "Departamento de MES");
        row = WritePortadaRow(sheet, row, "Aprobador:", "Reynaldo Barrera — MES Team Lead");
        row++;

        var seccion = sheet.Cell(row, 1);
        seccion.Value = "Datos de este reporte";
        seccion.Style.Font.Bold = true;
        seccion.Style.Font.FontSize = 13;
        seccion.Style.Font.FontColor = XLColor.FromHtml(StatusColorHelper.AccentHex);
        row += 2;

        var alcanceTexto = string.IsNullOrEmpty(filtro)
            ? "General — todos los modelos en Histórico (Estatus General Terminado)"
            : filtro;

        row = WritePortadaRow(sheet, row, "Alcance del reporte:", alcanceTexto);
        row = WritePortadaRow(sheet, row, "Modelos incluidos:", totalModelos.ToString());
        row = WritePortadaRow(sheet, row, "Fecha de generación:", $"{DateTime.Now:dd/MM/yyyy HH:mm}");
        WritePortadaRow(sheet, row, "Propósito:", "Antecedente de que la configuración de BD (iMES) y Plataforma iMES ya se realizó.");

        sheet.Column(1).Width = 22;
        sheet.Column(2).Width = 55;
    }

    // Una fila "Etiqueta: valor" de la portada.
    private static int WritePortadaRow(IXLWorksheet sheet, int row, string label, string value)
    {
        sheet.Cell(row, 1).Value = label;
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        sheet.Cell(row, 2).Value = value;
        sheet.Cell(row, 2).Style.Alignment.WrapText = true;
        return row + 1;
    }

    private static void BuildAltasSheet(XLWorkbook workbook, IReadOnlyList<ModelRecord> altas, string? filtro)
    {
        var sheet = workbook.Worksheets.Add("Altas de Modelos");

        var startRow = DrawBanner(
            sheet,
            "Alejandría — Seguimiento de Altas de Modelos",
            filtro,
            AltasColumnCount);

        string[] headers =
        {
            "ID", "Número de modelo", "Nombre de modelo", "Nº parte SMT", "Nº parte BE", "Nº parte TLA",
            "Quién recibe el documento", "Fecha de solicitud", "Quién mandó el documento"
        };

        var headerRow = startRow;
        DrawHeaderRow(sheet, headerRow, headers);

        var row = headerRow + 1;
        var firstDataRow = row;
        foreach (var alta in altas)
        {
            sheet.Cell(row, 1).Value = alta.Id;
            sheet.Cell(row, 2).Value = alta.NumeroModelo;
            sheet.Cell(row, 3).Value = alta.NombreModelo;
            sheet.Cell(row, 4).Value = alta.NumeroParteSmt;
            sheet.Cell(row, 5).Value = alta.NumeroParteBe;
            sheet.Cell(row, 6).Value = alta.NumeroParteTla;
            sheet.Cell(row, 7).Value = alta.QuienRecibeDocumento;

            var fechaCell = sheet.Cell(row, 8);
            fechaCell.Value = alta.FechaSolicitud;
            fechaCell.Style.DateFormat.Format = "dd/mm/yyyy";

            sheet.Cell(row, 9).Value = alta.QuienMandoDocumento;

            StripeRow(sheet, row, AltasColumnCount, row - firstDataRow);
            row++;
        }

        AddTableBorders(sheet, headerRow, row - 1, AltasColumnCount);
        sheet.Range(headerRow, 1, Math.Max(row - 1, headerRow), AltasColumnCount).SetAutoFilter();
        sheet.SheetView.FreezeRows(headerRow);
        sheet.SheetView.FreezeColumns(2);
        sheet.Columns().AdjustToContents();
    }

    private static void BuildModelosSheet(
        XLWorkbook workbook,
        IReadOnlyList<ModeloResumenItem> modelos,
        IReadOnlyDictionary<int, List<DetalleConfiguracion>> detallesPorModelo,
        string? filtro)
    {
        var sheet = workbook.Worksheets.Add("Modelos");

        var startRow = DrawBanner(
            sheet,
            "Alejandría — Configuración detallada por área",
            filtro,
            ModelosColumnCount);

        string[] headers =
        {
            "Número de modelo", "Nombre de modelo", "Área", "Números de parte",
            "MODELS_PARENTS (iMES)", "MODELS_MASTER (iMES)", "MODELS_SERIALNUMBERSPECS (iMES)", "MODELS_SERIALNUMBERSPECS Packing (iMES)",
            "Work Order", "Flujo", "Ruta", "Línea", "PartLineBomMaintenance",
            "Estatus BD (área)", "Estatus Plataforma (área)", "Estatus General (área)",
            "Estatus General (modelo completo)", "Fecha límite", "Días de retraso"
        };

        var headerRow = startRow;
        DrawHeaderRow(sheet, headerRow, headers);

        var row = headerRow + 1;
        var rowIndexInTable = 0;

        foreach (var modeloResumen in modelos)
        {
            var modelo = modeloResumen.Modelo;
            detallesPorModelo.TryGetValue(modelo.Id, out var detallesDelModelo);
            var detallesPorArea = (detallesDelModelo ?? new List<DetalleConfiguracion>()).ToDictionary(d => d.Area, d => d);

            // Una fila por cada área que el modelo tenga (SMT, BE y/o TLA) —
            // no solo la principal, igual que el reporte PDF.
            foreach (var area in modelo.AreasDisponibles)
            {
                detallesPorArea.TryGetValue(area, out var detalle);
                detalle ??= new DetalleConfiguracion { ModeloId = modelo.Id, Area = area };

                var resumenArea = ConfigStatusHelper.ComputeResumen(modelo, detalle);

                sheet.Cell(row, 1).Value = modelo.NumeroModelo;
                sheet.Cell(row, 2).Value = modelo.NombreModelo;
                sheet.Cell(row, 3).Value = area;
                sheet.Cell(row, 4).Value = modelo.GetNumeroParte(area);

                sheet.Cell(row, 5).Value = detalle.ImesModelsParents;
                sheet.Cell(row, 6).Value = detalle.ImesModelsMaster;
                sheet.Cell(row, 7).Value = detalle.ImesModelsSerialNumberSpecs;
                sheet.Cell(row, 8).Value = detalle.ImesModelsSerialNumberSpecsPacking;
                sheet.Cell(row, 9).Value = detalle.WorkOrder;
                sheet.Cell(row, 10).Value = detalle.Flujo;
                sheet.Cell(row, 11).Value = detalle.Ruta;
                sheet.Cell(row, 12).Value = detalle.Linea;
                sheet.Cell(row, 13).Value = detalle.PartLineBomMaintenance;

                WriteStatusCell(sheet.Cell(row, 14), resumenArea.EstatusBd);
                WriteStatusCell(sheet.Cell(row, 15), resumenArea.EstatusPlataforma);
                WriteStatusCell(sheet.Cell(row, 16), resumenArea.EstatusGeneral);
                WriteStatusCell(sheet.Cell(row, 17), modeloResumen.EstatusGeneral);

                var fechaCell = sheet.Cell(row, 18);
                fechaCell.Value = modeloResumen.FechaLimite;
                fechaCell.Style.DateFormat.Format = "dd/mm/yyyy";

                var retrasoCell = sheet.Cell(row, 19);
                retrasoCell.Value = modeloResumen.DiasRetraso;
                if (modeloResumen.EstaAtrasado)
                {
                    retrasoCell.Style.Font.FontColor = XLColor.FromHtml(StatusColorHelper.GetHex(ConfigStatusHelper.Bloqueado));
                    retrasoCell.Style.Font.Bold = true;
                }

                StripeRow(sheet, row, ModelosColumnCount, rowIndexInTable, skipColumns: new[] { 14, 15, 16, 17 });
                row++;
                rowIndexInTable++;
            }
        }

        AddTableBorders(sheet, headerRow, row - 1, ModelosColumnCount);
        sheet.Range(headerRow, 1, Math.Max(row - 1, headerRow), ModelosColumnCount).SetAutoFilter();
        sheet.SheetView.FreezeRows(headerRow);
        sheet.SheetView.FreezeColumns(2);
        sheet.Columns().AdjustToContents();
    }

    // Dibuja el banner azul. Devuelve la fila donde va el encabezado de la tabla.
    private static int DrawBanner(IXLWorksheet sheet, string titulo, string? filtro, int columnCount)
    {
        var lastColumnLetter = ColumnLetter(columnCount);

        sheet.Range($"A1:{lastColumnLetter}3").Merge();
        var bannerCell = sheet.Cell("A1");
        bannerCell.Style.Fill.BackgroundColor = XLColor.FromHtml(StatusColorHelper.AccentHex);
        bannerCell.Style.Font.FontColor = XLColor.White;
        bannerCell.Style.Font.Bold = true;
        bannerCell.Style.Font.FontSize = 16;
        bannerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        bannerCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        bannerCell.Style.Alignment.Indent = 2;
        bannerCell.Value = titulo;

        TryEmbedImage(sheet, "AppLogo.png", "A1", 24, 24);

        var subtitleRow = 4;
        var subtitulo = string.IsNullOrEmpty(filtro)
            ? $"Reporte general (Histórico) · Generado el {DateTime.Now:dd/MM/yyyy HH:mm}"
            : $"Reporte especializado — {filtro} · Generado el {DateTime.Now:dd/MM/yyyy HH:mm}";

        sheet.Range($"A{subtitleRow}:{lastColumnLetter}{subtitleRow}").Merge();
        var subtitleCell = sheet.Cell($"A{subtitleRow}");
        subtitleCell.Value = subtitulo;
        subtitleCell.Style.Font.Italic = true;
        subtitleCell.Style.Font.FontColor = XLColor.FromHtml("#64748B");

        sheet.Row(1).Height = 30;

        return subtitleRow + 2;
    }

    private static string ColumnLetter(int columnNumber) => ((char)('A' + columnNumber - 1)).ToString();

    private static void TryEmbedImage(IXLWorksheet sheet, string fileName, string cellAddress, double width, double height)
    {
        try
        {
            var imagePath = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
            if (!File.Exists(imagePath))
            {
                return;
            }

            using var stream = File.OpenRead(imagePath);
            sheet.AddPicture(stream)
                .MoveTo(sheet.Cell(cellAddress))
                .WithSize((int)width, (int)height);
        }
        catch (Exception)
        {
            // El logo es decorativo — si algo falla al insertarlo, el reporte sigue siendo válido sin él.
        }
    }

    private static void DrawHeaderRow(IXLWorksheet sheet, int rowIndex, string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(rowIndex, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(StatusColorHelper.AccentHex);
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        sheet.Row(rowIndex).Height = 20;
    }

    private static void WriteStatusCell(IXLCell cell, string status)
    {
        cell.Value = status;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Font.Bold = true;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(StatusColorHelper.GetHex(status));
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void StripeRow(IXLWorksheet sheet, int row, int columnCount, int rowIndexInTable, int[]? skipColumns = null)
    {
        if (rowIndexInTable % 2 == 0)
        {
            return;
        }

        for (var col = 1; col <= columnCount; col++)
        {
            if (skipColumns is not null && Array.IndexOf(skipColumns, col) >= 0)
            {
                continue;
            }

            sheet.Cell(row, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8FAFC");
        }
    }

    private static void AddTableBorders(IXLWorksheet sheet, int firstRow, int lastRow, int columnCount)
    {
        if (lastRow < firstRow)
        {
            return;
        }

        var range = sheet.Range(firstRow, 1, lastRow, columnCount);
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        range.Style.Border.OutsideBorderColor = XLColor.FromHtml(StatusColorHelper.AccentHex);
    }
}
