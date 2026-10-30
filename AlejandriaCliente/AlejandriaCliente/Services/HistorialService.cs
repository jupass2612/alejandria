using System.Text.Json;
using AlejandriaCliente.Models;

namespace AlejandriaCliente.Services;

// Historial local (por computadora) de las solicitudes generadas — un
// archivo historial.json con la lista de entradas, más una copia de
// respaldo de cada PDF en la misma carpeta.
public static class HistorialService
{
    private static readonly string CarpetaHistorial = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AlejandriaCliente", "historial");

    private static readonly string RutaManifiesto = Path.Combine(CarpetaHistorial, "historial.json");

    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = true };

    public static List<HistorialEntry> Listar()
    {
        try
        {
            if (!File.Exists(RutaManifiesto))
            {
                return new List<HistorialEntry>();
            }

            var json = File.ReadAllText(RutaManifiesto);
            var lista = JsonSerializer.Deserialize<List<HistorialEntry>>(json) ?? new List<HistorialEntry>();
            return lista.OrderByDescending(e => e.Fecha).ToList();
        }
        catch (Exception)
        {
            // Archivo corrupto o ilegible: se muestra el historial vacío
            // en vez de tronar la pantalla por esto.
            return new List<HistorialEntry>();
        }
    }

    // Guarda una entrada nueva, o actualiza la existente si ya había una
    // con el mismo Id (esto es lo que pasa cuando se edita y se vuelve a
    // generar). Además deja una copia de respaldo del PDF en la carpeta
    // del historial.
    public static void GuardarOActualizar(HistorialEntry entrada, string rutaPdfGenerado)
    {
        Directory.CreateDirectory(CarpetaHistorial);

        var rutaRespaldo = Path.Combine(CarpetaHistorial, $"{entrada.Id}.pdf");
        File.Copy(rutaPdfGenerado, rutaRespaldo, overwrite: true);
        entrada.RutaPdfRespaldo = rutaRespaldo;

        var lista = Listar();
        lista.RemoveAll(e => e.Id == entrada.Id);
        lista.Add(entrada);

        var json = JsonSerializer.Serialize(lista, OpcionesJson);
        File.WriteAllText(RutaManifiesto, json);
    }

    public static void Eliminar(string id)
    {
        var lista = Listar();
        var entrada = lista.FirstOrDefault(e => e.Id == id);
        if (entrada is null)
        {
            return;
        }

        lista.Remove(entrada);
        File.WriteAllText(RutaManifiesto, JsonSerializer.Serialize(lista, OpcionesJson));

        if (File.Exists(entrada.RutaPdfRespaldo))
        {
            File.Delete(entrada.RutaPdfRespaldo);
        }
    }
}
