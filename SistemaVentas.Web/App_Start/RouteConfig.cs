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
