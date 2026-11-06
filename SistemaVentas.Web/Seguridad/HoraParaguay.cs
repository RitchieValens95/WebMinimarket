using System;
using System.Globalization;

namespace SistemaVentas.Web.Seguridad
{
    public static class HoraParaguay
    {
        public static string Mostrar(DateTimeOffset? fecha)
        {
            if (!fecha.HasValue) return "Sin acceso registrado";
            TimeZoneInfo zona;
            try { zona = TimeZoneInfo.FindSystemTimeZoneById("Paraguay Standard Time"); }
            catch (TimeZoneNotFoundException) { zona = TimeZoneInfo.FindSystemTimeZoneById("America/Asuncion"); }
            return TimeZoneInfo.ConvertTime(fecha.Value, zona).ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("es-PY"));
        }
    }
}
