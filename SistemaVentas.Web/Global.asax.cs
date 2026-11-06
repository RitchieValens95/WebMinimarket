using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Web.SessionState;
using System.Web.Mvc;
using System.Web.Helpers;
using SistemaVentas.Web.Seguridad;
using System.Security.Principal;

namespace SistemaVentas.Web
{
    public class Global : HttpApplication
    {
        void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            var ruta = Request.AppRelativeCurrentExecutionFilePath ?? "";
            if (ruta.StartsWith("~/Content/", StringComparison.OrdinalIgnoreCase) ||
                ruta.StartsWith("~/Scripts/", StringComparison.OrdinalIgnoreCase) ||
                ruta.Equals("~/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
                ruta.TrimEnd('/').Equals("~/Acceso/Salir", StringComparison.OrdinalIgnoreCase)) return;
            if (Context.User == null || !(Context.User.Identity is FormsIdentity)) return;
            if (!Request.IsSecureConnection)
            {
                FormsAuthentication.SignOut();
                Context.User = new GenericPrincipal(new GenericIdentity(""), new string[0]);
                return;
            }
            try
            {
                // Valida la versión de sesión también al visitar páginas Web Forms.
                if (SeguridadWeb.Usuario(new HttpContextWrapper(Context)) == null)
                    Context.User = new GenericPrincipal(new GenericIdentity(""), new string[0]);
            }
            catch (Exception error)
            {
                System.Diagnostics.Trace.TraceError("Sesión no disponible: {0}", error.GetType().Name);
                Response.StatusCode = 503;
                Response.SuppressFormsAuthenticationRedirect = true;
                Response.TrySkipIisCustomErrors = true;
                Response.ContentType = "text/plain; charset=utf-8";
                Response.Write("El servicio no está disponible. Intenta nuevamente en unos momentos.");
                CompleteRequest();
            }
        }

        void Application_Start(object sender, EventArgs e)
        {
            // Código que se ejecuta al iniciar la aplicación
            AntiForgeryConfig.RequireSsl = true;
            AntiForgeryConfig.CookieName = ".SistemaVentas.Csrf";
            GlobalFilters.Filters.Add(new AutorizarSesionAttribute());
            GlobalFilters.Filters.Add(new ErroresSegurosAttribute());
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
