using System;
using System.Collections.Generic;

namespace SistemaVentas.Entidades
{
    public class OperacionNoPermitidaException : Exception
    {
        public OperacionNoPermitidaException(string mensaje) : base(mensaje) { }
    }

    public class UsuarioSesion
    {
        public long Id { get; set; }
        public string NombreUsuario { get; set; }
        public string Nombre { get; set; }
        public int VersionSesion { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
        public HashSet<string> Permisos { get; set; } = new HashSet<string>(StringComparer.Ordinal);
        public bool Puede(string permiso) { return Permisos.Contains(permiso); }
    }

    // Solo Datos y Negocio usan la credencial; nunca se envía a vistas ni a cookies.
    public class CredencialAcceso
    {
        public long Id { get; set; }
        public string Hash { get; set; }
        public bool Activo { get; set; }
        public bool Bloqueado { get; set; }
    }

    public class UsuarioAdministrable
    {
        public long Id { get; set; }
        public string NombreUsuario { get; set; }
        public string Nombre { get; set; }
        public string Correo { get; set; }
        public bool Activo { get; set; }
        public DateTimeOffset? UltimoAcceso { get; set; }
        public string Roles { get; set; }
        public List<long> RolIds { get; set; } = new List<long>();
    }

    public class SolicitudUsuario
    {
        public long Id { get; set; }
        public string NombreUsuario { get; set; }
        public string Nombre { get; set; }
        public string Correo { get; set; }
        public string Password { get; set; }
        public bool Activo { get; set; } = true;
        public long[] RolIds { get; set; } = new long[0];
    }

    public class RolSeguridad
    {
        public long Id { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public bool Activo { get; set; }
        public List<long> PermisoIds { get; set; } = new List<long>();
    }

    public class PermisoSeguridad
    {
        public long Id { get; set; }
        public string Codigo { get; set; }
        public string Modulo { get; set; }
        public string Descripcion { get; set; }
    }

    public class SolicitudRol
    {
        public long Id { get; set; }
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public bool Activo { get; set; } = true;
        public long[] PermisoIds { get; set; } = new long[0];
    }

    public class PaginaUsuarios
    {
        public List<UsuarioAdministrable> Usuarios { get; set; }
        public long Total { get; set; }
        public int Pagina { get; set; }
        public string Busqueda { get; set; }
        public int Tamano { get { return 20; } }
    }
}
