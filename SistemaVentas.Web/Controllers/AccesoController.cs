using System.Data.Common;
using System.Web.Mvc;
using SistemaVentas.Web.Seguridad;
using SistemaVentas.Web.ViewModels;

namespace SistemaVentas.Web.Controllers
{
    public class AccesoController : Controller
    {
        [AllowAnonymous, HttpGet, OutputCache(NoStore = true, Duration = 0, VaryByParam = "*")]
        public ActionResult Ingresar(string returnUrl)
        {
            if (!Request.IsSecureConnection) return new HttpStatusCodeResult(400, "Abre la dirección HTTPS de IIS Express.");
            if (SeguridadWeb.Usuario(HttpContext) != null) return RedirectToAction("Index", "Inicio");
            return View(new IngresoModelo { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public ActionResult Ingresar(IngresoModelo modelo)
        {
            if (!Request.IsSecureConnection) return new HttpStatusCodeResult(400, "Usa HTTPS para iniciar sesión.");
            Response.Cache.SetNoStore();
            if (!LimiteAccesos.Permitir(Request.UserHostAddress))
            {
                Response.StatusCode = 429;
                ModelState.AddModelError("", "Demasiados intentos. Espera un minuto antes de volver a intentar.");
            }
            if (ModelState.IsValid)
            {
                var usuario = Aplicacion.Servicio().Ingresar(modelo.Usuario, modelo.Password);
                if (usuario != null)
                {
                    SeguridadWeb.Iniciar(HttpContext, usuario);
                    if (Url.IsLocalUrl(modelo.ReturnUrl)) return Redirect(modelo.ReturnUrl);
                    return RedirectToAction("Index", "Inicio");
                }
                ModelState.AddModelError("", "Usuario o contraseña incorrectos. Si realizaste varios intentos, espera 15 minutos.");
            }
            modelo.Password = null;
            ModelState.SetModelValue("Password", new ValueProviderResult(null, null, System.Globalization.CultureInfo.InvariantCulture));
            return View(modelo);
        }

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public ActionResult Salir()
        {
            // Cerrar el acceso sigue siendo posible aunque PostgreSQL no responda.
            try
            {
                var actual = SeguridadWeb.Usuario(HttpContext);
                if (actual != null) Aplicacion.Servicio().RegistrarSalida(actual.Id, actual.VersionSesion);
            }
            catch (DbException) { System.Diagnostics.Trace.TraceWarning("No se pudo registrar la salida."); }
            finally { SeguridadWeb.Cerrar(HttpContext); }
            return RedirectToAction("Ingresar");
        }
    }
}
