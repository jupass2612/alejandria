namespace AlejandriaCliente.Models;

// Un documento generado, guardado localmente en esta computadora para
// poder volver a verlo o editarlo después.
public class HistorialEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public TipoSolicitud Tipo { get; set; }
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.Now;
    public string NombreModelo { get; set; } = string.Empty;

    // Ruta donde el usuario guardó el PDF la primera vez — al editar y
    // volver a generar, se sobreescribe aquí mismo en vez de pedir una
    // ubicación nueva.
    public string RutaPdf { get; set; } = string.Empty;

    // Copia de respaldo del PDF dentro de la carpeta del historial —
    // por si el archivo original se movió o se borró, "Ver PDF" todavía
    // tiene de dónde abrirlo.
    public string RutaPdfRespaldo { get; set; } = string.Empty;

    // Los datos del formulario, en JSON — así "Editar" puede volver a
    // llenar todos los campos tal como quedaron.
    public string DatosJson { get; set; } = string.Empty;

    public string TipoTexto => Tipo switch
    {
        TipoSolicitud.AltaDeModelo => "Alta de modelo",
        TipoSolicitud.CambioDeFlujo => "Cambio de flujo",
        TipoSolicitud.CambioDeModelo => "Cambio de modelo",
        _ => Tipo.ToString()
    };

    public string FechaTexto => Fecha.ToString("d/M/yyyy HH:mm");
}
