using System.Web.Mvc;

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
    }
}
