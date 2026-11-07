using System;
using System.Collections.Generic;
using Npgsql;
using SistemaVentas.Entidades;

namespace SistemaVentas.Datos
{
    public abstract class RepositorioCatalogo : IDisposable
    {
        private readonly TipoCatalogo tipo;
        private readonly string tabla;
        private readonly NpgsqlConnection conexion;
        private readonly NpgsqlTransaction transaccion;

        protected RepositorioCatalogo(string cadena, TipoCatalogo catalogo)
        {
            tipo = catalogo;
            // La tabla proviene de esta lista cerrada, nunca de un parámetro HTTP.
            switch (tipo)
            {
                case TipoCatalogo.Categorias: tabla = "public.categorias"; break;
                case TipoCatalogo.Marcas: tabla = "public.marcas"; break;
                default: throw new ArgumentOutOfRangeException("catalogo");
            }
            conexion = ConexionPostgreSql.Abrir(cadena);
            try { transaccion = conexion.BeginTransaction(); }
            catch { conexion.Dispose(); throw; }
        }

        private NpgsqlCommand Comando(string sql, params object[] pares)
        {
            var cmd = new NpgsqlCommand(sql, conexion, transaccion) { CommandTimeout = 10 };
            for (int i=0; i<pares.Length; i+=2) cmd.Parameters.AddWithValue((string)pares[i], pares[i+1] ?? DBNull.Value);
            return cmd;
        }

        public bool Puede(long actor, string permiso)
        {
            using (var cmd = Comando(@"SELECT EXISTS(SELECT 1 FROM public.usuarios u
                JOIN public.usuarios_roles ur ON ur.usuario_id=u.id JOIN public.roles r ON r.id=ur.rol_id
                JOIN public.roles_permisos rp ON rp.rol_id=r.id JOIN public.permisos p ON p.id=rp.permiso_id
                WHERE u.id=@actor AND u.activo AND r.activo AND p.codigo=@permiso)", "actor", actor, "permiso", permiso))
                return (bool)cmd.ExecuteScalar();
        }

        private RegistroCatalogo Leer(NpgsqlDataReader lector)
        {
            RegistroCatalogo registro = tipo == TipoCatalogo.Categorias ? (RegistroCatalogo)new Categoria() : new Marca();
            registro.Id=lector.GetInt64(0); registro.Nombre=lector.GetString(1); registro.Activo=lector.GetBoolean(2); registro.Version=lector.GetInt64(3);
            return registro;
        }

        public RegistroCatalogo Obtener(long id, bool bloquear = false)
        {
            using (var cmd = Comando("SELECT id,nombre,activo,xmin::text::bigint FROM " + tabla + " WHERE id=@id" + (bloquear ? " FOR UPDATE" : ""), "id", id))
            using (var lector = cmd.ExecuteReader()) return lector.Read() ? Leer(lector) : null;
        }

        public PaginaCatalogo Listar(int pagina, string busqueda, string estado)
        {
            const string filtro = " WHERE nombre ILIKE @q AND (@estado='todos' OR activo=(@estado='activos'))";
            long total;
            using (var cmd = Comando("SELECT count(*) FROM " + tabla + filtro, "q", "%"+busqueda+"%", "estado", estado)) total=(long)cmd.ExecuteScalar();
            pagina=Math.Min(pagina,(int)Math.Max(1, Math.Min(1000000,(total+19)/20)));
            var lista=new List<RegistroCatalogo>();
            using (var cmd=Comando("SELECT id,nombre,activo,xmin::text::bigint FROM "+tabla+filtro+" ORDER BY lower(nombre),id LIMIT 20 OFFSET @offset", "q", "%"+busqueda+"%", "estado", estado,"offset",(pagina-1)*20))
            using (var lector=cmd.ExecuteReader()) while(lector.Read()) lista.Add(Leer(lector));
            return new PaginaCatalogo { Registros=lista,Total=total,Pagina=pagina,Busqueda=busqueda,Estado=estado };
        }

        public void ExigirVersion()
        {
            using(var cmd=Comando("SELECT EXISTS(SELECT 1 FROM public.esquema_versiones WHERE version='006')"))
                if(!(bool)cmd.ExecuteScalar()) throw new OperacionNoPermitidaException("Esta función requiere una actualización de la aplicación. Contacta al administrador.");
        }

        public long Insertar(string nombre, bool activo)
        {
            try
            {
                using(var cmd=Comando("INSERT INTO "+tabla+"(nombre,activo) VALUES (@nombre,@activo) RETURNING id", "nombre",nombre,"activo",activo))
                    return (long)cmd.ExecuteScalar();
            }
            catch(PostgresException error) when(error.SqlState=="23505")
            { throw new OperacionNoPermitidaException("Ya existe un registro con ese nombre, incluso si está inactivo. Puedes editarlo o reactivarlo."); }
        }

        public void Actualizar(long id,string nombre,bool activo,long version)
        {
            try
            {
                using(var cmd=Comando("UPDATE "+tabla+" SET nombre=@nombre,activo=@activo WHERE id=@id AND xmin::text::bigint=@version RETURNING id", "id",id,"nombre",nombre,"activo",activo,"version",version))
                    if(cmd.ExecuteScalar()==null) throw new OperacionNoPermitidaException("El registro cambió en otra ventana. Recarga la página antes de volver a guardar.");
            }
            catch(PostgresException error) when(error.SqlState=="23505")
            { throw new OperacionNoPermitidaException("Ya existe un registro con ese nombre, incluso si está inactivo."); }
            catch(PostgresException error) when(error.SqlState=="23503" &&
                (error.ConstraintName=="fk_productos_categoria_activa" || error.ConstraintName=="fk_productos_marca_activa"))
            { throw new OperacionNoPermitidaException("Hay productos activos asociados. Reasígnalos o desactívalos antes de desactivar este registro."); }
        }

        public void Auditar(long actor,string accion,long id)
        {
            using(var cmd=Comando("INSERT INTO public.auditoria(usuario_id,accion,entidad,entidad_id) VALUES (@actor,@accion,@entidad,@id)",
                "actor",actor,"accion",accion,"entidad",tipo==TipoCatalogo.Categorias?"categorias":"marcas","id",id)) cmd.ExecuteNonQuery();
        }
        public void Confirmar() { transaccion.Commit(); }
        public void Dispose() { transaccion.Dispose(); conexion.Dispose(); }
    }
}
