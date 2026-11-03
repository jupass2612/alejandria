namespace Alejandria.Services;

// Plazo en días hábiles y días de retraso
public static class BusinessDayHelper
{
    // Suma días hábiles (sin sábado ni domingo)
    public static DateTime AddBusinessDays(DateTime start, int businessDays)
    {
        var date = start.Date;
        var added = 0;

        while (added < businessDays)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                added++;
            }
        }

        return date;
    }

    // 0 si ya está terminada o no se vence
    public static int DiasDeRetraso(DateTime fechaLimite, bool terminado, DateTime? hoy = null)
    {
        if (terminado)
        {
            return 0;
        }

        var referencia = (hoy ?? DateTime.Now).Date;
        var diferencia = (referencia - fechaLimite.Date).Days;
        return diferencia > 0 ? diferencia : 0;
    }
}
