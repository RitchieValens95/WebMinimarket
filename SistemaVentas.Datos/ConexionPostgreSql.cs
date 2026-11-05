using System;
using Npgsql;
using SistemaVentas.Entidades;

namespace SistemaVentas.Datos
{
    public class ConexionPostgreSql
    {
        private readonly string cadenaConexion;

        public ConexionPostgreSql(string cadenaConexion)
        {
            this.cadenaConexion = cadenaConexion;
        }

        public ResultadoConexion Comprobar()
        {
            if (string.IsNullOrWhiteSpace(cadenaConexion))
            {
                return Fallo("Falta configurar la conexión local a PostgreSQL.");
            }

            try
            {
                // Los límites evitan que la prueba quede esperando indefinidamente.
                var configuracion = new NpgsqlConnectionStringBuilder(cadenaConexion);
                configuracion.Timeout = 5;
                configuracion.CommandTimeout = 10;

                // using cierra y libera la conexión incluso si ocurre un error.
                using (var conexion = new NpgsqlConnection(configuracion.ConnectionString))
                {
                    conexion.Open();
                    const string sql = "SELECT version(), current_setting('server_version_num')::integer";
                    using (var comando = new NpgsqlCommand(sql, conexion))
                    using (var lector = comando.ExecuteReader())
                    {
                        if (!lector.Read())
                        {
                            return Fallo("PostgreSQL no devolvió su versión.");
                        }

                        return new ResultadoConexion
                        {
                            Exito = true,
                            VersionServidor = lector.GetString(0),
                            VersionPrincipal = lector.GetInt32(1) / 10000
                        };
                    }
                }
            }
            catch (PostgresException error)
            {
                // Solo mostramos mensajes controlados, nunca la cadena ni la contraseña.
                switch (error.SqlState)
                {
                    case "28P01":
                    case "28000":
                        return Fallo("PostgreSQL rechazó la autenticación. Revisa el usuario, la contraseña y los permisos de acceso.");
                    case "3D000":
                        return Fallo("La base de datos configurada no existe. Revisa su nombre.");
                    case "42501":
                        return Fallo("El usuario no tiene permiso para acceder a la base de datos.");
                    default:
                        return Fallo("PostgreSQL rechazó la comprobación de conexión.");
                }
            }
            catch (NpgsqlException)
            {
                return Fallo("No se pudo completar la conexión. Revisa que PostgreSQL esté iniciado, el host, el puerto y la configuración de conexión.");
            }
            catch (TimeoutException)
            {
                return Fallo("Se agotó el tiempo de espera al consultar PostgreSQL.");
            }
            catch (ArgumentException)
            {
                return Fallo("La configuración local de conexión tiene un formato o un valor inválido.");
            }
        }

        private static ResultadoConexion Fallo(string mensaje)
        {
            return new ResultadoConexion { Exito = false, Mensaje = mensaje };
        }
    }
}
