namespace Alejandria.Services;

// Avisos cortos abajo que se quitan solos; MainWindow los muestra
public static class ToastService
{
    public enum Tipo
    {
        Exito,
        Info,
        Error
    }

    private static Action<string, Tipo>? _mostrar;

    public static void Registrar(Action<string, Tipo> mostrar) => _mostrar = mostrar;

    public static void Mostrar(string? mensaje, Tipo tipo = Tipo.Exito)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return;
        }

        _mostrar?.Invoke(mensaje, tipo);
    }
}
