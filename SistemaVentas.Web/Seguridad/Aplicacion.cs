using System.Configuration;
using SistemaVentas.Negocio;

namespace SistemaVentas.Web.Seguridad
{
    public static class Aplicacion
    {
        public static ServicioSeguridad Servicio()
        { return new ServicioSeguridad(Cadena()); }

        public static ServicioCategorias Categorias()
        { return new ServicioCategorias(Cadena()); }

        public static ServicioMarcas Marcas()
        { return new ServicioMarcas(Cadena()); }

        private static string Cadena()
        {
            var cadena = ConfigurationManager.AppSettings["SistemaVentas.PostgreSql"];
            if (string.IsNullOrWhiteSpace(cadena)) throw new ConfigurationErrorsException("Falta la configuración local de conexión.");
            return cadena;
        }
    }
}
