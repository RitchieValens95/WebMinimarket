using SistemaVentas.Datos;
using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public class DiagnosticoConexion
    {
        public ResultadoConexion Comprobar(string cadenaConexion)
        {
            var resultado = new ConexionPostgreSql(cadenaConexion).Comprobar();

            if (resultado.Exito)
            {
                // Este proyecto requiere la versión principal 18 del servidor.
                resultado.Exito = resultado.VersionPrincipal == 18;
                resultado.Mensaje = resultado.Exito
                    ? "Conexión correcta con PostgreSQL 18."
                    : "La conexión funciona, pero este sistema requiere PostgreSQL 18.";
            }

            return resultado;
        }
    }
}
