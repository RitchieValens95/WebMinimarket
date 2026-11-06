using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SistemaVentas.Datos;
using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public sealed class ServicioSeguridad
    {
        private readonly string cadena;
        private static readonly string HashSinCuenta = CrearHashSinCuenta();
        public ServicioSeguridad(string cadenaConexion) { cadena = cadenaConexion; }

        private static string CrearHashSinCuenta()
        {
            var bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return Contrasenas.Crear(Convert.ToBase64String(bytes));
        }

        public UsuarioSesion Ingresar(string usuario, string password)
        {
            usuario = (usuario ?? "").Trim();
            if (usuario.Length > 80 || password == null || password.Length > 128) return null;
            using (var db = new RepositorioSeguridad(cadena))
            {
                var credencial = db.BuscarCredencial(usuario);
                var hash = credencial != null && credencial.Activo && !credencial.Bloqueado ? credencial.Hash : HashSinCuenta;
                bool valido = Contrasenas.Verificar(password, hash);
                if (credencial == null || !credencial.Activo || credencial.Bloqueado || !valido)
                {
                    if (credencial == null || !credencial.Bloqueado) db.AccesoFallido(credencial == null ? (long?)null : credencial.Id);
                    db.Confirmar();
                    return null;
                }
                db.AccesoCorrecto(credencial.Id);
                var sesion = db.ObtenerSesion(credencial.Id);
                db.Confirmar();
                return sesion;
            }
        }

        public UsuarioSesion ObtenerSesion(long id, int version)
        {
            if (id <= 0 || version <= 0) return null;
            using (var db = new RepositorioSeguridad(cadena))
            {
                var usuario = db.ObtenerSesion(id);
                return usuario != null && usuario.VersionSesion == version ? usuario : null;
            }
        }

        private static void Exigir(RepositorioSeguridad db, long actor, string permiso)
        {
            var usuario = db.ObtenerSesion(actor);
            if (usuario == null || !usuario.Puede(permiso))
                throw new OperacionNoPermitidaException("No tienes permiso para realizar esta operación.");
        }

        private static void ConservarAdministrador(RepositorioSeguridad db)
        {
            if (!db.ExisteAdministradorEfectivo())
                throw new OperacionNoPermitidaException("Debe permanecer al menos un administrador activo con permisos para administrar usuarios y roles.");
        }

        public PaginaUsuarios ListarUsuarios(long actor, int pagina, string busqueda)
        {
            busqueda = (busqueda ?? "").Trim();
            if (busqueda.Length > 100) busqueda = busqueda.Substring(0, 100);
            using (var db = new RepositorioSeguridad(cadena))
            {
                Exigir(db, actor, "USUARIOS_VER");
                return db.ListarUsuarios(Math.Max(1, Math.Min(pagina, 1000000)), busqueda);
            }
        }

        public UsuarioAdministrable ObtenerUsuario(long actor, long id)
        {
            using (var db = new RepositorioSeguridad(cadena))
            { Exigir(db, actor, "USUARIOS_ADMINISTRAR"); return db.ObtenerUsuario(id); }
        }

        public List<RolSeguridad> RolesParaUsuario(long actor)
        {
            using (var db = new RepositorioSeguridad(cadena))
            { Exigir(db, actor, "USUARIOS_ADMINISTRAR"); return db.ListarRoles(); }
        }

        public List<RolSeguridad> ListarRoles(long actor)
        {
            using (var db = new RepositorioSeguridad(cadena))
            { Exigir(db, actor, "ROLES_ADMINISTRAR"); return db.ListarRoles(); }
        }

        public List<PermisoSeguridad> ListarPermisos(long actor)
        {
            using (var db = new RepositorioSeguridad(cadena))
            { Exigir(db, actor, "ROLES_ADMINISTRAR"); return db.ListarPermisos(); }
        }

        public long GuardarUsuario(long actor, SolicitudUsuario usuario)
        {
            ValidarUsuario(usuario);
            if (usuario.Activo && usuario.RolIds.Length == 0)
                throw new OperacionNoPermitidaException("Asigna al menos un rol al usuario activo.");
            using (var db = new RepositorioSeguridad(cadena))
            {
                db.BloquearAdministracion();
                Exigir(db, actor, "USUARIOS_ADMINISTRAR");
                if (!db.RolesValidos(usuario.RolIds)) throw new OperacionNoPermitidaException("Selecciona roles existentes y activos.");
                var id = db.GuardarUsuario(usuario, string.IsNullOrEmpty(usuario.Password) ? null : Contrasenas.Crear(usuario.Password));
                ConservarAdministrador(db);
                db.Auditar(actor, usuario.Id == 0 ? "USUARIO_CREADO" : "USUARIO_EDITADO", "usuarios", id);
                db.Confirmar();
                return id;
            }
        }

        public long GuardarRol(long actor, SolicitudRol rol)
        {
            if (rol == null || rol.Id < 0) throw new OperacionNoPermitidaException("Rol inválido.");
            rol.Codigo = (rol.Codigo ?? "").Trim().ToUpperInvariant();
            rol.Nombre = (rol.Nombre ?? "").Trim();
            rol.PermisoIds = (rol.PermisoIds ?? new long[0]).Distinct().ToArray();
            if (!Regex.IsMatch(rol.Codigo, "^[A-Z][A-Z0-9_]{1,39}$") || rol.Nombre.Length < 2 || rol.Nombre.Length > 80)
                throw new OperacionNoPermitidaException("Revisa el código y el nombre del rol.");
            using (var db = new RepositorioSeguridad(cadena))
            {
                db.BloquearAdministracion();
                Exigir(db, actor, "ROLES_ADMINISTRAR");
                if (!db.PermisosValidos(rol.PermisoIds)) throw new OperacionNoPermitidaException("Hay permisos no válidos.");
                var id = db.GuardarRol(rol);
                ConservarAdministrador(db);
                db.Auditar(actor, rol.Id == 0 ? "ROL_CREADO" : "ROL_EDITADO", "roles", id);
                db.Confirmar();
                return id;
            }
        }

        public void DesactivarUsuario(long actor, long id)
        {
            using (var db = new RepositorioSeguridad(cadena))
            {
                db.BloquearAdministracion();
                Exigir(db, actor, "USUARIOS_ADMINISTRAR");
                bool cambio = db.DesactivarUsuario(id);
                if (!cambio && db.ObtenerUsuario(id) == null) throw new OperacionNoPermitidaException("El usuario no existe.");
                ConservarAdministrador(db);
                if (cambio) db.Auditar(actor, "USUARIO_DESACTIVADO", "usuarios", id);
                db.Confirmar();
            }
        }

        public long CrearPrimerAdministrador(string nombreUsuario, string nombre, string password)
        {
            var usuario = new SolicitudUsuario { NombreUsuario = nombreUsuario, Nombre = nombre, Password = password, Activo = true };
            ValidarUsuario(usuario);
            using (var db = new RepositorioSeguridad(cadena))
            {
                db.BloquearAdministracion();
                if (db.HayUsuarios()) throw new OperacionNoPermitidaException("Ya existen usuarios. El administrador inicial solo se crea en una instalación sin cuentas.");
                var rol = db.ListarRoles().SingleOrDefault(r => r.Codigo == "ADMINISTRADOR" && r.Activo);
                if (rol == null) throw new OperacionNoPermitidaException("Falta el rol Administrador activo.");
                usuario.RolIds = new[] { rol.Id };
                var id = db.GuardarUsuario(usuario, Contrasenas.Crear(password));
                ConservarAdministrador(db);
                db.Auditar(id, "ADMINISTRADOR_INICIAL", "usuarios", id);
                db.Confirmar();
                return id;
            }
        }

        public void RegistrarSalida(long actor, int version)
        {
            using (var db = new RepositorioSeguridad(cadena))
            {
                if (db.InvalidarSesiones(actor, version)) db.Auditar(actor, "LOGOUT", "usuarios", actor);
                db.Confirmar();
            }
        }

        private static void ValidarUsuario(SolicitudUsuario usuario)
        {
            if (usuario == null || usuario.Id < 0) throw new OperacionNoPermitidaException("Usuario inválido.");
            usuario.NombreUsuario = (usuario.NombreUsuario ?? "").Trim();
            usuario.Nombre = (usuario.Nombre ?? "").Trim();
            usuario.Correo = string.IsNullOrWhiteSpace(usuario.Correo) ? null : usuario.Correo.Trim();
            usuario.RolIds = (usuario.RolIds ?? new long[0]).Distinct().ToArray();
            if (!Regex.IsMatch(usuario.NombreUsuario, "^[A-Za-z0-9_.-]{3,80}$") || usuario.Nombre.Length < 2 || usuario.Nombre.Length > 150)
                throw new OperacionNoPermitidaException("Usa un nombre de acceso de 3 a 80 letras sin acentos, números, puntos, guiones o guiones bajos, y completa el nombre de la persona.");
            if (usuario.Id == 0 && string.IsNullOrEmpty(usuario.Password)) throw new OperacionNoPermitidaException("La contraseña es obligatoria al crear un usuario.");
            if (!string.IsNullOrEmpty(usuario.Password) && (usuario.Password.Length < 12 || usuario.Password.Length > 128))
                throw new OperacionNoPermitidaException("La contraseña debe tener entre 12 y 128 caracteres.");
            if (usuario.Id != 0 && usuario.Activo && usuario.RolIds.Length == 0)
                throw new OperacionNoPermitidaException("Asigna al menos un rol al usuario activo.");
            if (usuario.Correo != null)
            {
                try { if (usuario.Correo.Length > 254 || new MailAddress(usuario.Correo).Address != usuario.Correo) throw new FormatException(); }
                catch (FormatException) { throw new OperacionNoPermitidaException("El correo no es válido."); }
            }
        }
    }
}
