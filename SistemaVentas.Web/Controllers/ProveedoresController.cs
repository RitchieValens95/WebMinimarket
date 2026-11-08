using System.Web.Mvc;
using SistemaVentas.Entidades;
using SistemaVentas.Web.Seguridad;
using SistemaVentas.Web.ViewModels;

namespace SistemaVentas.Web.Controllers
{
    [Permiso("PROVEEDORES_ADMINISTRAR")]
    public class ProveedoresController : Controller
    {
        private long Actor{get{return SeguridadWeb.Usuario(HttpContext).Id;}}
        [HttpGet]
        public ActionResult Index(int pagina=1,string busqueda="",string estado="todos")
        {return View(Aplicacion.Proveedores().Listar(Actor,pagina,busqueda,estado));}
        [HttpGet]
        public ActionResult Detalle(long id)
        {
            var proveedor=Aplicacion.Proveedores().Obtener(Actor,id);
            if(proveedor==null)return HttpNotFound();
            return View(proveedor);
        }
        [HttpGet]
        public ActionResult Crear(){return View("Formulario",new ProveedorFormulario());}
        [HttpGet]
        public ActionResult Editar(long id)
        {
            var proveedor=Aplicacion.Proveedores().Obtener(Actor,id);
            if(proveedor==null)return HttpNotFound();
            return View("Formulario",ProveedorFormulario.Desde(proveedor));
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Guardar([Bind(Include="Id,Version,Nombre,Ruc,Telefono,Direccion,Correo,Activo")] ProveedorFormulario modelo)
        {
            if(ModelState.IsValid)
            {
                try
                {
                    long id=Aplicacion.Proveedores().Guardar(Actor,modelo.Solicitud());
                    TempData["Exito"]="Proveedor guardado correctamente.";return RedirectToAction("Detalle",new{id=id});
                }
                catch(OperacionNoPermitidaException e){ModelState.AddModelError("",e.Message);}
            }
            return View("Formulario",modelo);
        }
        [HttpPost,ValidateAntiForgeryToken]
        public ActionResult Desactivar(long id,long version)
        {
            try{Aplicacion.Proveedores().Desactivar(Actor,id,version);TempData["Exito"]="Proveedor desactivado. Sus compras se conservan.";}
            catch(OperacionNoPermitidaException e){TempData["Error"]=e.Message;}
            return RedirectToAction("Index");
        }
        [HttpGet,Permiso("COMPRAS_ADMINISTRAR")]
        public ActionResult Historial(long id,int pagina=1)
        {
            var historial=Aplicacion.Proveedores().Historial(Actor,id,pagina);
            if(historial==null)return HttpNotFound();
            return View(historial);
        }
    }
}
