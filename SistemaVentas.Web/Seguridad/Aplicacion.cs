using System.Configuration;
using SistemaVentas.Negocio;

namespace SistemaVentas.Web.Seguridad
{
    public static class Aplicacion
    {
        public static ServicioSeguridad Servicio()
        {
            var cadena = ConfigurationManager.AppSettings["SistemaVentas.PostgreSql"];
            if (string.IsNullOrWhiteSpace(cadena)) throw new ConfigurationErrorsException("Falta la configuración local de conexión.");
            return new ServicioSeguridad(cadena);
        }
    }
}
