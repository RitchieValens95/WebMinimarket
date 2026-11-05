using System.Configuration;
using System.Web.Mvc;
using SistemaVentas.Negocio;

namespace SistemaVentas.Web.Controllers
{
    public class InicioController : Controller
    {
        [HttpGet]
        public ActionResult Index()
        {
            // Por convención, MVC busca Views/Inicio/Index.cshtml.
            return View();
        }

        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Conexion()
        {
            // Diagnóstico de desarrollo: no se publica para clientes remotos.
            if (!Request.IsLocal || !HttpContext.IsDebuggingEnabled)
            {
                return HttpNotFound();
            }

            var cadena = ConfigurationManager.AppSettings["SistemaVentas.PostgreSql"];
            var resultado = new DiagnosticoConexion().Comprobar(cadena);
            return View(resultado);
        }
    }
}
