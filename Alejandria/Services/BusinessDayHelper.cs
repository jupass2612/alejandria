namespace Alejandria.Services;

// Cálculo del plazo (días hábiles) y de los días de retraso una vez que se vence.
public static class BusinessDayHelper
{
    // Suma N días hábiles (sin sábado ni domingo).
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

    // Días de retraso. 0 si ya está Terminada o si todavía no se vence.
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
