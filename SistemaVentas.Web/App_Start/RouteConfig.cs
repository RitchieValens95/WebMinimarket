using System.Web.Mvc;
using System.Web.Routing;
using Microsoft.AspNet.FriendlyUrls;

namespace SistemaVentas.Web
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute("Acceso", "Acceso/{action}", new { controller = "Acceso", action = "Ingresar" });
            routes.MapRoute("Usuarios", "Usuarios/{action}/{id}", new { controller = "Usuarios", action = "Index", id = UrlParameter.Optional });
            routes.MapRoute("Roles", "Roles/{action}/{id}", new { controller = "Roles", action = "Index", id = UrlParameter.Optional });
            routes.MapRoute("Categorias", "Categorias/{action}/{id}", new { controller = "Categorias", action = "Index", id = UrlParameter.Optional });
            routes.MapRoute("Marcas", "Marcas/{action}/{id}", new { controller = "Marcas", action = "Index", id = UrlParameter.Optional });

            routes.MapRoute("Productos", "Productos/{action}/{id}", new { controller = "Productos", action = "Index", id = UrlParameter.Optional });

            routes.MapRoute("Clientes", "Clientes/{action}/{id}", new { controller = "Clientes", action = "Index", id = UrlParameter.Optional });

            // MVC atiende el inicio antes de que FriendlyUrls busque una página .aspx.
            routes.MapRoute(
                name: "InicioMvc",
                url: "",
                defaults: new { controller = "Inicio", action = "Index" });

            routes.MapRoute(
                name: "InicioAcciones",
                url: "Inicio/{action}",
                defaults: new { controller = "Inicio", action = "Index" });

            // Conservamos las rutas Web Forms, por ejemplo /About y /Contact.
            var settings = new FriendlyUrlSettings();
            settings.AutoRedirectMode = RedirectMode.Permanent;
            routes.EnableFriendlyUrls(settings);
        }
    }
}
