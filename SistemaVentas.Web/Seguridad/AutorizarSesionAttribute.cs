using System;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace SistemaVentas.Web.Seguridad
{
    public class AutorizarSesionAttribute : AuthorizeAttribute
    {
        private readonly string permiso;
        public AutorizarSesionAttribute() { }
        protected AutorizarSesionAttribute(string requerido) { permiso = requerido; }

        public override void OnAuthorization(AuthorizationContext contexto)
        {
            if (contexto.ActionDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true) ||
                contexto.ActionDescriptor.ControllerDescriptor.IsDefined(typeof(AllowAnonymousAttribute), true)) return;
            if (!contexto.HttpContext.Request.IsSecureConnection)
            {
                contexto.Result = new HttpStatusCodeResult(400, "Usa la dirección HTTPS de la aplicación.");
                return;
            }
            contexto.HttpContext.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            contexto.HttpContext.Response.Cache.SetNoStore();
            try
            {
                var usuario = SeguridadWeb.Usuario(contexto.HttpContext);
                if (usuario == null)
                {
                    contexto.HttpContext.Response.SuppressFormsAuthenticationRedirect = true;
                    contexto.Result = new RedirectToRouteResult(new RouteValueDictionary(new
                    { controller = "Acceso", action = "Ingresar", returnUrl = contexto.HttpContext.Request.RawUrl }));
                }
                else if (permiso != null && !usuario.Puede(permiso))
                {
                    contexto.HttpContext.Response.SuppressFormsAuthenticationRedirect = true;
                    contexto.Result = new HttpStatusCodeResult(403, "No tienes permiso para acceder a este módulo.");
                }
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceError("Validación de sesión: {0}", error.GetType().Name);
                contexto.HttpContext.Response.SuppressFormsAuthenticationRedirect = true;
                contexto.Result = new HttpStatusCodeResult(503, "No se pudo comprobar la sesión. Revisa la disponibilidad del servicio.");
            }
        }
    }

    public sealed class PermisoAttribute : AutorizarSesionAttribute
    {
        public PermisoAttribute(string codigo) : base(codigo) { }
    }
}
