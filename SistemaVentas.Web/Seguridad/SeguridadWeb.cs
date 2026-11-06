using System;
using System.Security.Principal;
using System.Web;
using System.Web.Security;
using SistemaVentas.Entidades;

namespace SistemaVentas.Web.Seguridad
{
    public static class SeguridadWeb
    {
        private const string Clave = "SistemaVentas.UsuarioActual";

        public static UsuarioSesion Usuario(HttpContextBase contexto)
        {
            if (contexto.Items.Contains(Clave)) return contexto.Items[Clave] as UsuarioSesion;
            UsuarioSesion usuario = null;
            var identidad = contexto.User == null ? null : contexto.User.Identity as FormsIdentity;
            long id;
            int version;
            if (identidad != null && !identidad.Ticket.Expired && long.TryParse(identidad.Name, out id) && int.TryParse(identidad.Ticket.UserData, out version))
                usuario = Aplicacion.Servicio().ObtenerSesion(id, version);
            contexto.Items[Clave] = usuario;
            if (usuario != null) contexto.User = new GenericPrincipal(identidad, usuario.Roles.ToArray());
            else if (identidad != null)
            {
                FormsAuthentication.SignOut();
                contexto.User = new GenericPrincipal(new GenericIdentity(""), new string[0]);
            }
            return usuario;
        }

        public static void Iniciar(HttpContextBase contexto, UsuarioSesion usuario)
        {
            Cerrar(contexto);
            var ahora = DateTime.Now;
            var ticket = new FormsAuthenticationTicket(2, usuario.Id.ToString(), ahora, ahora.Add(FormsAuthentication.Timeout), false, usuario.VersionSesion.ToString(), FormsAuthentication.FormsCookiePath);
            contexto.Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName, FormsAuthentication.Encrypt(ticket))
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = FormsAuthentication.FormsCookiePath
            });
        }

        public static void Cerrar(HttpContextBase contexto)
        {
            FormsAuthentication.SignOut();
            if (contexto.Session != null) { contexto.Session.Clear(); contexto.Session.Abandon(); }
            contexto.Response.Cookies.Add(new HttpCookie("ASP.NET_SessionId", "")
            { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Expires = DateTime.UtcNow.AddDays(-1), Path = "/" });
        }
    }
}
