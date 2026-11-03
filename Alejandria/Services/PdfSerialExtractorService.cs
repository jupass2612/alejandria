using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace Alejandria.Services;

// Serial del PDF con su etiqueta si trae (FG, PCBA, Clock spring...)
public record SerialCandidato(string? Etiqueta, string Valor)
{
    public string EtiquetaMostrada => Etiqueta ?? "Serial";
}

// Saca los seriales de ejemplo del PDF ("Laser Serial Number real example").
// Puede haber varios; por el OCR se buscan tokens con pinta de serial después de la etiqueta
public static class PdfSerialExtractorService
{
    private static readonly Regex CandidatoRegex = new(
        @"(?:(?<etiqueta>FG|PCBA|T&C|Clock\s*spring|PCB)\s*:\s*)?(?<valor>[A-Za-z0-9][A-Za-z0-9\-]{5,}(?::[A-Za-z0-9][A-Za-z0-9\-]*)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CorteSeccionRegex = new(@"\b3\.\s", RegexOptions.Compiled);

    // Lista vacía si no existe, no se lee o no está la sección
    public static List<SerialCandidato> ExtraerCandidatosSerial(string rutaPdf)
    {
        if (string.IsNullOrWhiteSpace(rutaPdf) || !File.Exists(rutaPdf))
        {
            return new List<SerialCandidato>();
        }

        string texto;
        try
        {
            using var documento = PdfDocument.Open(rutaPdf);
            texto = string.Join(" ", documento.GetPages().Select(pagina => pagina.Text));
        }
        catch (Exception)
        {
            // PDF dañado, protegido o escaneo sin texto
            return new List<SerialCandidato>();
        }

        var idxEtiqueta = texto.IndexOf("Laser Serial Number", StringComparison.OrdinalIgnoreCase);
        if (idxEtiqueta < 0)
        {
            return new List<SerialCandidato>();
        }

        var resto = texto[idxEtiqueta..];
        var corte = CorteSeccionRegex.Match(resto);
        var ventana = corte.Success ? resto[..corte.Index] : resto[..Math.Min(600, resto.Length)];

        var crudos = new List<(string? Etiqueta, string Valor, int Inicio, int Fin)>();
        foreach (Match match in CandidatoRegex.Matches(ventana))
        {
            var valor = match.Groups["valor"].Value;
            if (valor.Count(char.IsDigit) < 3)
            {
                // Palabras en inglés que el OCR tomó como token
                continue;
            }

            var etiqueta = match.Groups["etiqueta"].Success
                ? NormalizarEtiqueta(match.Groups["etiqueta"].Value)
                : null;

            crudos.Add((etiqueta, valor, match.Index, match.Index + match.Length));
        }

        // Un pedazo sin etiqueta pegado al anterior se junta (espacios de más del OCR)
        var candidatos = new List<(string? Etiqueta, string Valor, int Inicio, int Fin)>();
        foreach (var (etiqueta, valor, inicio, fin) in crudos)
        {
            if (etiqueta is null && candidatos.Count > 0 && inicio - candidatos[^1].Fin <= 3)
            {
                var anterior = candidatos[^1];
                candidatos[^1] = (anterior.Etiqueta, $"{anterior.Valor} {valor}", anterior.Inicio, fin);
            }
            else
            {
                candidatos.Add((etiqueta, valor, inicio, fin));
            }
        }

        return candidatos
            .Select(c => new SerialCandidato(c.Etiqueta, c.Valor))
            .DistinctBy(c => c.Valor)
            .ToList();
    }

    private static string NormalizarEtiqueta(string etiqueta) =>
        Regex.Replace(etiqueta.Trim(), @"\s+", " ");
}
