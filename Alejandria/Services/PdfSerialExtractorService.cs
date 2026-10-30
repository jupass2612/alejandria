using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace Alejandria.Services;

// Un serial encontrado en "Laser Serial Number real example", con su
// etiqueta si el PDF traía una (FG, PCBA, T&C, Clock spring...).
public record SerialCandidato(string? Etiqueta, string Valor)
{
    public string EtiquetaMostrada => Etiqueta ?? "Serial";
}

// Saca los seriales de ejemplo del PDF adjunto a un modelo. El campo
// "Laser Serial Number real example" no siempre trae un solo valor:
// puede venir suelto, con el número de parte del cliente y dos puntos, o
// varios valores con su propia etiqueta (FG/PCBA/Clock spring) cuando el
// modelo tiene más de un componente. Por eso ExtraerCandidatosSerial
// devuelve una lista — si sale más de uno, se le pregunta al usuario cuál.
//
// El texto puede traer ruido de OCR si el PDF viene de un escaneo, así
// que no se busca la etiqueta exacta del campo: se toma una ventana de
// texto después de "Laser Serial Number" y ahí se buscan tokens con
// pinta de serial (mayúsculas/dígitos/guion/dos puntos, con al menos 3
// dígitos, para descartar palabras sueltas en inglés).
public static class PdfSerialExtractorService
{
    private static readonly Regex CandidatoRegex = new(
        @"(?:(?<etiqueta>FG|PCBA|T&C|Clock\s*spring|PCB)\s*:\s*)?(?<valor>[A-Za-z0-9][A-Za-z0-9\-]{5,}(?::[A-Za-z0-9][A-Za-z0-9\-]*)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CorteSeccionRegex = new(@"\b3\.\s", RegexOptions.Compiled);

    // Lista vacía si el archivo no existe, no se pudo leer, o no se encontró la sección.
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
            // PDF dañado, protegido, o un escaneo sin texto real.
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
                // Descarta palabras sueltas en inglés que el OCR confundió con un token válido.
                continue;
            }

            var etiqueta = match.Groups["etiqueta"].Success
                ? NormalizarEtiqueta(match.Groups["etiqueta"].Value)
                : null;

            crudos.Add((etiqueta, valor, match.Index, match.Index + match.Length));
        }

        // Un fragmento sin etiqueta se pega al candidato anterior si están
        // casi juntos en el texto (pasa cuando el OCR mete un espacio de
        // más a la mitad de un mismo valor). Si el siguiente sí trae su
        // propia etiqueta, no se pega: es un valor nuevo.
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
