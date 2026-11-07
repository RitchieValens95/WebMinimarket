using System.Web.Mvc;
using SistemaVentas.Entidades;
using SistemaVentas.Negocio;
using SistemaVentas.Web.Seguridad;
using SistemaVentas.Web.ViewModels;

namespace SistemaVentas.Web.Controllers
{
    public abstract class CatalogosControllerBase : Controller
    {
        protected abstract ServicioCatalogo Servicio { get; }
        protected abstract string NombreCatalogo { get; }
        private long Actor { get { return SeguridadWeb.Usuario(HttpContext).Id; } }

        [HttpGet]
        public ActionResult Index(int pagina = 1, string busqueda = "", string estado = "todos")
        {
            PrepararVista();
            return View("~/Views/Catalogos/Index.cshtml", new CatalogoListado { Datos = Servicio.Listar(Actor, pagina, busqueda, estado) });
        }

        [HttpGet]
        public ActionResult Crear() { return Formulario(new CatalogoFormulario()); }

        [HttpGet]
        public ActionResult Editar(long id)
        {
            var registro = Servicio.Obtener(Actor, id);
            if (registro == null) return HttpNotFound();
            return Formulario(new CatalogoFormulario { Id = registro.Id, Nombre = registro.Nombre, Activo = registro.Activo, Version = registro.Version });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Guardar(CatalogoFormulario modelo)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Servicio.Guardar(Actor, modelo.Id, modelo.Nombre, modelo.Activo, modelo.Version);
                    TempData["Exito"] = "Registro guardado correctamente.";
                    return RedirectToAction("Index");
                }
                catch (OperacionNoPermitidaException error) { ModelState.AddModelError("", error.Message); }
            }
            return Formulario(modelo);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Desactivar(long id, long version)
        {
            try
            {
                Servicio.Desactivar(Actor, id, version);
                TempData["Exito"] = "Registro desactivado. Puedes reactivarlo desde Editar.";
            }
            catch (OperacionNoPermitidaException error) { TempData["Error"] = error.Message; }
            return RedirectToAction("Index");
        }

        private void PrepararVista()
        {
            // Estos valores pertenecen al controlador, nunca al formulario recibido.
            ViewBag.Catalogo = NombreCatalogo;
            ViewBag.Controlador = ControllerContext.RouteData.Values["controller"].ToString();
        }
        private ActionResult Formulario(CatalogoFormulario modelo)
        { PrepararVista(); return View("~/Views/Catalogos/Formulario.cshtml", modelo); }
    }
}
