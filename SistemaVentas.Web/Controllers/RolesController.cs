using System.Linq;
using System.Web.Mvc;
using SistemaVentas.Entidades;
using SistemaVentas.Web.Seguridad;
using SistemaVentas.Web.ViewModels;

namespace SistemaVentas.Web.Controllers
{
    [Permiso("ROLES_ADMINISTRAR")]
    public class RolesController : Controller
    {
        private long Actor { get { return SeguridadWeb.Usuario(HttpContext).Id; } }
        [HttpGet]
        public ActionResult Index(int pagina = 1, string busqueda = "")
        {
            busqueda = (busqueda ?? "").Trim();
            if (busqueda.Length > 100) busqueda = busqueda.Substring(0, 100);
            var filtrados = Aplicacion.Servicio().ListarRoles(Actor).Where(r =>
                r.Nombre.IndexOf(busqueda, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.Codigo.IndexOf(busqueda, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            pagina = System.Math.Max(1, System.Math.Min(pagina, System.Math.Max(1, (filtrados.Count + 19) / 20)));
            return View(new RolesListado { Roles = filtrados.Skip((pagina - 1) * 20).Take(20).ToList(), Total = filtrados.Count, Pagina = pagina, Busqueda = busqueda });
        }
        [HttpGet] public ActionResult Crear() { return Formulario(new RolFormulario()); }
        [HttpGet] public ActionResult Editar(long id)
        {
            var rol = Aplicacion.Servicio().ListarRoles(Actor).SingleOrDefault(r => r.Id == id);
            if (rol == null) return HttpNotFound();
            return Formulario(new RolFormulario { Id = rol.Id, Codigo = rol.Codigo, Nombre = rol.Nombre, Activo = rol.Activo, PermisoIds = rol.PermisoIds.ToArray() });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Guardar(RolFormulario modelo)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    Aplicacion.Servicio().GuardarRol(Actor, new SolicitudRol { Id = modelo.Id, Codigo = modelo.Codigo, Nombre = modelo.Nombre,
                        Activo = modelo.Activo, PermisoIds = modelo.PermisoIds });
                    TempData["Exito"] = "Rol guardado. Sus permisos se comprobarán de nuevo en la siguiente solicitud.";
                    return RedirectToAction("Index");
                }
                catch (OperacionNoPermitidaException error) { ModelState.AddModelError("", error.Message); }
            }
            return Formulario(modelo);
        }

        private ActionResult Formulario(RolFormulario modelo)
        { modelo.PermisosDisponibles = Aplicacion.Servicio().ListarPermisos(Actor); return View("Formulario", modelo); }
    }
}
