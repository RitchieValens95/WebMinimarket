using System.Linq;
using System.Web.Mvc;
using SistemaVentas.Entidades;
using SistemaVentas.Web.Seguridad;
using SistemaVentas.Web.ViewModels;

namespace SistemaVentas.Web.Controllers
{
    public class UsuariosController : Controller
    {
        private long Actor { get { return SeguridadWeb.Usuario(HttpContext).Id; } }

        [HttpGet, Permiso("USUARIOS_VER")]
        public ActionResult Index(int pagina = 1, string busqueda = "")
        { return View(Aplicacion.Servicio().ListarUsuarios(Actor, pagina, busqueda)); }

        [HttpGet, Permiso("USUARIOS_ADMINISTRAR")]
        public ActionResult Crear()
        { return Formulario(new UsuarioFormulario()); }

        [HttpGet, Permiso("USUARIOS_ADMINISTRAR")]
        public ActionResult Editar(long id)
        {
            var usuario = Aplicacion.Servicio().ObtenerUsuario(Actor, id);
            if (usuario == null) return HttpNotFound();
            return Formulario(new UsuarioFormulario { Id = usuario.Id, NombreUsuario = usuario.NombreUsuario,
                Nombre = usuario.Nombre, Correo = usuario.Correo, Activo = usuario.Activo, RolIds = usuario.RolIds.ToArray() });
        }

        [HttpPost, ValidateAntiForgeryToken, Permiso("USUARIOS_ADMINISTRAR")]
        public ActionResult Guardar(UsuarioFormulario modelo)
        {
            if (modelo.Id == 0 && string.IsNullOrEmpty(modelo.Password)) ModelState.AddModelError("Password", "La contraseña es obligatoria.");
            if (ModelState.IsValid)
            {
                try
                {
                    Aplicacion.Servicio().GuardarUsuario(Actor, new SolicitudUsuario { Id = modelo.Id, NombreUsuario = modelo.NombreUsuario,
                        Nombre = modelo.Nombre, Correo = modelo.Correo, Activo = modelo.Activo, Password = modelo.Password, RolIds = modelo.RolIds });
                    TempData["Exito"] = "Usuario guardado. Sus sesiones anteriores quedaron invalidadas.";
                    return RedirectToAction("Index");
                }
                catch (OperacionNoPermitidaException error) { ModelState.AddModelError("", error.Message); }
            }
            modelo.Password = modelo.Confirmacion = null;
            ModelState.SetModelValue("Password", new ValueProviderResult(null, null, System.Globalization.CultureInfo.InvariantCulture));
            ModelState.SetModelValue("Confirmacion", new ValueProviderResult(null, null, System.Globalization.CultureInfo.InvariantCulture));
            return Formulario(modelo);
        }

        [HttpPost, ValidateAntiForgeryToken, Permiso("USUARIOS_ADMINISTRAR")]
        public ActionResult Desactivar(long id)
        {
            try { Aplicacion.Servicio().DesactivarUsuario(Actor, id); TempData["Exito"] = "Usuario desactivado."; }
            catch (OperacionNoPermitidaException error) { TempData["Error"] = error.Message; }
            return RedirectToAction("Index");
        }

        private ActionResult Formulario(UsuarioFormulario modelo)
        {
            modelo.RolesDisponibles = Aplicacion.Servicio().RolesParaUsuario(Actor);
            return View("Formulario", modelo);
        }
    }
}
