using System.Web.Mvc;
using SistemaVentas.Entidades;
using SistemaVentas.Web.Seguridad;
using SistemaVentas.Web.ViewModels;

namespace SistemaVentas.Web.Controllers
{
    [Permiso("CLIENTES_VER")]
    public class ClientesController : Controller
    {
        private long Actor { get { return SeguridadWeb.Usuario(HttpContext).Id; } }
        [HttpGet]
        public ActionResult Index(int pagina=1,string busqueda="",string estado="todos")
        { return View(Aplicacion.Clientes().Listar(Actor,pagina,busqueda,estado)); }
        [HttpGet]
        public ActionResult Detalle(long id)
        {
            var cliente=Aplicacion.Clientes().Obtener(Actor,id);
            if(cliente==null) return HttpNotFound();
            return View(cliente);
        }
        [HttpGet,Permiso("CLIENTES_REGISTRAR")]
        public ActionResult Crear(){return Formulario(new ClienteFormulario());}
        [HttpGet,Permiso("CLIENTES_EDITAR")]
        public ActionResult Editar(long id)
        {
            var cliente=Aplicacion.Clientes().Obtener(Actor,id);
            if(cliente==null) return HttpNotFound();
            if(cliente.EsGenerico) return Generico(id);
            return Formulario(ClienteFormulario.Desde(cliente));
        }
        [HttpPost,ValidateAntiForgeryToken,Permiso("CLIENTES_REGISTRAR")]
        public ActionResult Registrar([Bind(Include="Nombre,TipoDocumento,Documento,Telefono,Direccion,Correo,Activo")] ClienteFormulario modelo)
        {
            modelo.Id=0;modelo.Version=0;
            return GuardarFormulario(modelo);
        }
        [HttpPost,ValidateAntiForgeryToken,Permiso("CLIENTES_EDITAR")]
        public ActionResult Guardar([Bind(Include="Id,Version,Nombre,TipoDocumento,Documento,Telefono,Direccion,Correo,Activo")] ClienteFormulario modelo)
        {
            if(modelo.Id<=0) return new HttpStatusCodeResult(400,"Selecciona un cliente existente para editar.");
            return GuardarFormulario(modelo);
        }
        [HttpPost,ValidateAntiForgeryToken,Permiso("CLIENTES_EDITAR")]
        public ActionResult Desactivar(long id,long version)
        {
            try{Aplicacion.Clientes().Desactivar(Actor,id,version);TempData["Exito"]="Cliente desactivado. Su historial se conserva.";}
            catch(OperacionNoPermitidaException e){TempData["Error"]=e.Message;}
            return RedirectToAction("Index");
        }
        [HttpGet]
        public ActionResult Historial(long id,int pagina=1)
        {
            var usuario=SeguridadWeb.Usuario(HttpContext);
            if(!usuario.Puede("VENTAS_VER_TODAS") && !usuario.Puede("VENTAS_VER_PROPIAS")) return new HttpStatusCodeResult(403,"No tienes permiso para consultar ventas del cliente.");
            var historial=Aplicacion.Clientes().Historial(Actor,id,pagina);
            if(historial==null) return HttpNotFound();
            return View(historial);
        }
        private ActionResult GuardarFormulario(ClienteFormulario modelo)
        {
            if(ModelState.IsValid)
            {
                try
                {
                    long id=Aplicacion.Clientes().Guardar(Actor,modelo.Solicitud());
                    TempData["Exito"]="Cliente guardado correctamente.";return RedirectToAction("Detalle",new{id=id});
                }
                catch(OperacionNoPermitidaException e){ModelState.AddModelError("",e.Message);}
            }
            return Formulario(modelo);
        }
        private ActionResult Formulario(ClienteFormulario modelo)
        { return View("Formulario",modelo); }
        private ActionResult Generico(long id)
        { TempData["Error"]="Consumidor final es el cliente genérico del sistema. Sus datos y su estado se conservan.";return RedirectToAction("Detalle",new{id=id}); }
    }
}
