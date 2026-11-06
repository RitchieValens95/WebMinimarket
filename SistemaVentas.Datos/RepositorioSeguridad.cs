using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using NpgsqlTypes;
using SistemaVentas.Entidades;

namespace SistemaVentas.Datos
{
    // Cada instancia comparte una conexión y una transacción. Sin Confirmar se revierte.
    public sealed class RepositorioSeguridad : IDisposable
    {
        private readonly NpgsqlConnection conexion;
        private readonly NpgsqlTransaction transaccion;

        public RepositorioSeguridad(string cadena)
        {
            conexion = ConexionPostgreSql.Abrir(cadena);
            try { transaccion = conexion.BeginTransaction(); }
            catch { conexion.Dispose(); throw; }
        }

        private NpgsqlCommand Comando(string sql, params object[] pares)
        {
            var cmd = new NpgsqlCommand(sql, conexion, transaccion) { CommandTimeout = 10 };
            for (int i = 0; i < pares.Length; i += 2)
            {
                var nombre = (string)pares[i];
                var valor = pares[i + 1];
                if (valor is long[]) cmd.Parameters.AddWithValue(nombre, NpgsqlDbType.Array | NpgsqlDbType.Bigint, valor);
                else cmd.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
            }
            return cmd;
        }

        private object Escalar(string sql, params object[] pares)
        {
            try { using (var cmd = Comando(sql, pares)) return cmd.ExecuteScalar(); }
            catch (PostgresException e) when (e.SqlState == "23505")
            { throw new OperacionNoPermitidaException("Ya existe un registro con ese nombre de usuario o código."); }
        }

        private void Ejecutar(string sql, params object[] pares)
        {
            try { using (var cmd = Comando(sql, pares)) cmd.ExecuteNonQuery(); }
            catch (PostgresException e) when (e.SqlState == "23505")
            { throw new OperacionNoPermitidaException("Ya existe un registro con ese nombre de usuario o código."); }
        }

        public void BloquearAdministracion()
        { Escalar("SELECT pg_advisory_xact_lock(783144982301)"); }

        public bool HayUsuarios()
        { return (bool)Escalar("SELECT EXISTS(SELECT 1 FROM public.usuarios)"); }

        public CredencialAcceso BuscarCredencial(string nombre)
        {
            using (var cmd = Comando("SELECT id,password_hash,activo,coalesce(bloqueado_hasta>CURRENT_TIMESTAMP,false) FROM public.usuarios WHERE lower(btrim(nombre_usuario))=lower(@nombre) FOR UPDATE", "nombre", nombre))
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                return new CredencialAcceso { Id = r.GetInt64(0), Hash = r.GetString(1), Activo = r.GetBoolean(2), Bloqueado = r.GetBoolean(3) };
            }
        }

        public void AccesoFallido(long? id)
        {
            if (id.HasValue)
                Ejecutar(@"UPDATE public.usuarios SET
                  bloqueado_hasta=CASE WHEN (CASE WHEN bloqueado_hasta<=CURRENT_TIMESTAMP THEN 1 ELSE least(intentos_fallidos+1,5) END)>=5
                    THEN CURRENT_TIMESTAMP+interval '15 minutes' ELSE NULL END,
                  intentos_fallidos=CASE WHEN bloqueado_hasta<=CURRENT_TIMESTAMP THEN 1 ELSE least(intentos_fallidos+1,5) END
                  WHERE id=@id", "id", id.Value);
            Auditar(id, "LOGIN_FALLIDO", "usuarios", id);
        }

        public void AccesoCorrecto(long id)
        {
            Ejecutar("UPDATE public.usuarios SET ultimo_acceso=CURRENT_TIMESTAMP,intentos_fallidos=0,bloqueado_hasta=NULL WHERE id=@id", "id", id);
            Auditar(id, "LOGIN_CORRECTO", "usuarios", id);
        }

        public UsuarioSesion ObtenerSesion(long id)
        {
            UsuarioSesion sesion;
            using (var cmd = Comando("SELECT id,nombre_usuario,nombre,version_sesion FROM public.usuarios WHERE id=@id AND activo", "id", id))
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                sesion = new UsuarioSesion { Id = r.GetInt64(0), NombreUsuario = r.GetString(1), Nombre = r.GetString(2), VersionSesion = r.GetInt32(3) };
            }
            using (var cmd = Comando("SELECT r.codigo FROM public.roles r JOIN public.usuarios_roles ur ON ur.rol_id=r.id WHERE ur.usuario_id=@id AND r.activo ORDER BY r.codigo", "id", id))
            using (var r = cmd.ExecuteReader()) while (r.Read()) sesion.Roles.Add(r.GetString(0));
            using (var cmd = Comando(@"SELECT DISTINCT p.codigo FROM public.permisos p
                JOIN public.roles_permisos rp ON rp.permiso_id=p.id JOIN public.roles r ON r.id=rp.rol_id
                JOIN public.usuarios_roles ur ON ur.rol_id=r.id WHERE ur.usuario_id=@id AND r.activo", "id", id))
            using (var r = cmd.ExecuteReader()) while (r.Read()) sesion.Permisos.Add(r.GetString(0));
            return sesion;
        }

        private const string ColumnasUsuario = @"u.id,u.nombre_usuario,u.nombre,u.correo,u.activo,u.ultimo_acceso,
            ARRAY(SELECT ur.rol_id FROM public.usuarios_roles ur WHERE ur.usuario_id=u.id ORDER BY ur.rol_id),
            coalesce((SELECT string_agg(r.nombre,', ' ORDER BY r.nombre) FROM public.roles r JOIN public.usuarios_roles ur ON ur.rol_id=r.id WHERE ur.usuario_id=u.id),'')";

        private static UsuarioAdministrable LeerUsuario(NpgsqlDataReader r)
        {
            return new UsuarioAdministrable { Id = r.GetInt64(0), NombreUsuario = r.GetString(1), Nombre = r.GetString(2),
                Correo = r.IsDBNull(3) ? null : r.GetString(3), Activo = r.GetBoolean(4),
                UltimoAcceso = r.IsDBNull(5) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(5),
                RolIds = r.GetFieldValue<long[]>(6).ToList(), Roles = r.GetString(7) };
        }

        public UsuarioAdministrable ObtenerUsuario(long id)
        {
            using (var cmd = Comando("SELECT " + ColumnasUsuario + " FROM public.usuarios u WHERE id=@id", "id", id))
            using (var r = cmd.ExecuteReader()) return r.Read() ? LeerUsuario(r) : null;
        }

        public PaginaUsuarios ListarUsuarios(int pagina, string busqueda)
        {
            const string filtro = " FROM public.usuarios u WHERE (u.nombre_usuario ILIKE @q OR u.nombre ILIKE @q)";
            var total = (long)Escalar("SELECT count(*)" + filtro, "q", "%" + busqueda + "%");
            pagina = Math.Min(pagina, (int)Math.Max(1, Math.Min(1000000, (total + 19) / 20)));
            var lista = new List<UsuarioAdministrable>();
            using (var cmd = Comando("SELECT " + ColumnasUsuario + filtro + " ORDER BY lower(u.nombre_usuario),u.id LIMIT 20 OFFSET @offset", "q", "%" + busqueda + "%", "offset", (pagina - 1) * 20))
            using (var r = cmd.ExecuteReader()) while (r.Read()) lista.Add(LeerUsuario(r));
            return new PaginaUsuarios { Usuarios = lista, Total = total, Pagina = pagina, Busqueda = busqueda };
        }

        public List<RolSeguridad> ListarRoles()
        {
            var lista = new List<RolSeguridad>();
            using (var cmd = Comando(@"SELECT r.id,r.codigo,r.nombre,r.activo,
                ARRAY(SELECT permiso_id FROM public.roles_permisos WHERE rol_id=r.id ORDER BY permiso_id)
                FROM public.roles r ORDER BY r.codigo"))
            using (var r = cmd.ExecuteReader()) while (r.Read()) lista.Add(new RolSeguridad { Id = r.GetInt64(0), Codigo = r.GetString(1), Nombre = r.GetString(2), Activo = r.GetBoolean(3), PermisoIds = r.GetFieldValue<long[]>(4).ToList() });
            return lista;
        }

        public List<PermisoSeguridad> ListarPermisos()
        {
            var lista = new List<PermisoSeguridad>();
            using (var cmd = Comando("SELECT id,codigo,modulo,descripcion FROM public.permisos ORDER BY modulo,codigo"))
            using (var r = cmd.ExecuteReader()) while (r.Read()) lista.Add(new PermisoSeguridad { Id = r.GetInt64(0), Codigo = r.GetString(1), Modulo = r.GetString(2), Descripcion = r.GetString(3) });
            return lista;
        }

        public bool RolesValidos(long[] ids)
        { return (long)Escalar("SELECT count(*) FROM public.roles WHERE id=ANY(@ids) AND activo", "ids", ids) == ids.Length; }
        public bool PermisosValidos(long[] ids)
        { return (long)Escalar("SELECT count(*) FROM public.permisos WHERE id=ANY(@ids)", "ids", ids) == ids.Length; }

        public long GuardarUsuario(SolicitudUsuario u, string hash)
        {
            long id;
            if (u.Id == 0)
                id = (long)Escalar("INSERT INTO public.usuarios(nombre_usuario,nombre,correo,activo,password_hash) VALUES (@usuario,@nombre,@correo,@activo,@hash) RETURNING id",
                    "usuario", u.NombreUsuario, "nombre", u.Nombre, "correo", u.Correo, "activo", u.Activo, "hash", hash);
            else
            {
                var obtenido = Escalar(@"UPDATE public.usuarios SET nombre_usuario=@usuario,nombre=@nombre,correo=@correo,activo=@activo,
                  password_hash=coalesce(@hash,password_hash),version_sesion=version_sesion+1,
                  intentos_fallidos=CASE WHEN @hash IS NOT NULL THEN 0 ELSE intentos_fallidos END,
                  bloqueado_hasta=CASE WHEN @hash IS NOT NULL THEN NULL ELSE bloqueado_hasta END WHERE id=@id RETURNING id",
                    "usuario", u.NombreUsuario, "nombre", u.Nombre, "correo", u.Correo, "activo", u.Activo, "hash", hash, "id", u.Id);
                if (obtenido == null) throw new OperacionNoPermitidaException("El usuario no existe.");
                id = (long)obtenido;
            }
            Ejecutar("DELETE FROM public.usuarios_roles WHERE usuario_id=@id", "id", id);
            foreach (long rol in u.RolIds) Ejecutar("INSERT INTO public.usuarios_roles(usuario_id,rol_id) VALUES (@id,@rol)", "id", id, "rol", rol);
            return id;
        }

        public long GuardarRol(SolicitudRol rol)
        {
            long id;
            if (rol.Id == 0)
                id = (long)Escalar("INSERT INTO public.roles(codigo,nombre,activo) VALUES (@codigo,@nombre,@activo) RETURNING id", "codigo", rol.Codigo, "nombre", rol.Nombre, "activo", rol.Activo);
            else
            {
                var obtenido = Escalar("UPDATE public.roles SET nombre=@nombre,activo=@activo WHERE id=@id AND codigo=@codigo RETURNING id", "nombre", rol.Nombre, "activo", rol.Activo, "id", rol.Id, "codigo", rol.Codigo);
                if (obtenido == null) throw new OperacionNoPermitidaException("El rol no existe o se intentó cambiar su código.");
                id = (long)obtenido;
            }
            Ejecutar("DELETE FROM public.roles_permisos WHERE rol_id=@id", "id", id);
            foreach (long permiso in rol.PermisoIds) Ejecutar("INSERT INTO public.roles_permisos(rol_id,permiso_id) VALUES (@id,@permiso)", "id", id, "permiso", permiso);
            return id;
        }

        public bool DesactivarUsuario(long id)
        { return Escalar("UPDATE public.usuarios SET activo=false,version_sesion=version_sesion+1 WHERE id=@id AND activo RETURNING id", "id", id) != null; }

        public bool InvalidarSesiones(long id, int version)
        { return Escalar("UPDATE public.usuarios SET version_sesion=version_sesion+1 WHERE id=@id AND version_sesion=@version RETURNING id", "id", id, "version", version) != null; }

        public bool ExisteAdministradorEfectivo()
        {
            return (bool)Escalar(@"SELECT EXISTS(SELECT 1 FROM public.usuarios u JOIN public.usuarios_roles ur ON ur.usuario_id=u.id
                JOIN public.roles r ON r.id=ur.rol_id WHERE u.activo AND r.activo AND r.codigo='ADMINISTRADOR'
                AND (SELECT count(DISTINCT p.codigo) FROM public.roles_permisos rp JOIN public.permisos p ON p.id=rp.permiso_id
                     WHERE rp.rol_id=r.id AND p.codigo IN ('USUARIOS_VER','USUARIOS_ADMINISTRAR','ROLES_ADMINISTRAR'))=3)");
        }

        public void Auditar(long? actor, string accion, string entidad, long? id)
        { Ejecutar("INSERT INTO public.auditoria(usuario_id,accion,entidad,entidad_id) VALUES (@actor,@accion,@entidad,@id)", "actor", actor, "accion", accion, "entidad", entidad, "id", id); }
        public void Confirmar() { transaccion.Commit(); }
        public void Dispose() { transaccion.Dispose(); conexion.Dispose(); }
    }
}
