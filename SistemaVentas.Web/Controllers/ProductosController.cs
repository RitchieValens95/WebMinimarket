using System.Web.Mvc;
using SistemaVentas.Negocio;
using SistemaVentas.Entidades;
using SistemaVentas.Web.Seguridad;
using SistemaVentas.Web.ViewModels;

namespace SistemaVentas.Web.Controllers
{
    [Permiso("PRODUCTOS_VER")]
    public class ProductosController : Controller
    {
        private long Actor { get { return SeguridadWeb.Usuario(HttpContext).Id; } }
        [HttpGet]
        public ActionResult Index(FiltroProductos filtro)
        {
            var servicio=Aplicacion.Productos();
            return View(new ProductosListado { Datos=servicio.Listar(Actor,filtro),Opciones=servicio.Opciones(Actor) });
        }
        [HttpGet]
        public ActionResult Detalle(long id)
        {
            var producto=Aplicacion.Productos().Obtener(Actor,id);
            if(producto==null) return HttpNotFound();
            return View(producto);
        }
        [HttpGet,Permiso("PRODUCTOS_EDITAR")]
        public ActionResult Crear() { return RedirectToAction("Index"); }
        [HttpPost,ValidateAntiForgeryToken,Permiso("PRODUCTOS_EDITAR")]
        public ActionResult Nuevo()
        {
            long id=Aplicacion.Productos().ReservarNuevo(Actor);
            return Formulario(new ProductoFormulario { IdNuevo=id,Codigo=id.ToString(System.Globalization.CultureInfo.InvariantCulture),Reserva=ReservaNuevoProducto.Crear(id,Actor) });
        }
        [HttpGet,Permiso("PRODUCTOS_EDITAR")]
        public ActionResult Editar(long id)
        {
            var producto=Aplicacion.Productos().Obtener(Actor,id);
            if(producto==null) return HttpNotFound();
            return Formulario(ProductoFormulario.Desde(producto));
        }
        [HttpPost,ValidateAntiForgeryToken,Permiso("PRODUCTOS_EDITAR")]
        public ActionResult Guardar([Bind(Include="Id,Version,Codigo,Descripcion,CategoriaId,MarcaId,UnidadMedidaId,PermiteDecimales,PrecioCompra,PrecioVenta,StockMinimo,Activo,CodigoBarra,GenerarCodigoBarras,BalanzaPesable,BalanzaUnitario,PorcentajeIva,Reserva")] ProductoFormulario modelo)
        {
            if(modelo.Id==0)
            {
                try { modelo.IdNuevo=ReservaNuevoProducto.Leer(modelo.Reserva,Actor); }
                catch(OperacionNoPermitidaException e) { ModelState.AddModelError("",e.Message); }
            }
            var solicitud=modelo.Solicitud(ModelState);
            if(ModelState.IsValid)
            {
                try
                {
                    long id=Aplicacion.Productos().Guardar(Actor,solicitud);
                    TempData["Exito"]="Producto guardado correctamente.";
                    return RedirectToAction("Detalle",new{id=id});
                }
                catch(OperacionNoPermitidaException e) { ModelState.AddModelError("",e.Message); }
            }
            return Formulario(modelo);
        }
        [HttpPost,ValidateAntiForgeryToken,Permiso("PRODUCTOS_EDITAR")]
        public ActionResult Desactivar(long id,long version)
        {
            try { Aplicacion.Productos().Desactivar(Actor,id,version);TempData["Exito"]="Producto desactivado. Conserva sus existencias e historial."; }
            catch(OperacionNoPermitidaException e) { TempData["Error"]=e.Message; }
            return RedirectToAction("Index");
        }
        [HttpGet,Permiso("STOCK_VER")]
        public ActionResult Movimientos(long id,int pagina=1)
        {
            var historial=Aplicacion.Productos().Historial(Actor,id,pagina);
            if(historial==null) return HttpNotFound();
            return View(historial);
        }
        private ActionResult Formulario(ProductoFormulario modelo)
        {
            var servicio=Aplicacion.Productos();
            modelo.Opciones=servicio.Opciones(Actor,modelo.CategoriaId,modelo.MarcaId,paraFormulario:true);
            // El stock se vuelve a leer; no se acepta como un dato enviado por el navegador.
            var actual=modelo.Id>0?servicio.Obtener(Actor,modelo.Id):null;
            if(modelo.Id>0 && actual==null) return HttpNotFound();
            modelo.StockActual=actual==null?0:actual.StockActual;
            long idCodigo=actual==null?modelo.IdNuevo:actual.Id;
            modelo.CodigoGenerado=idCodigo>0 && idCodigo<=9999999999L?Ean13.Generar(idCodigo):"";
            modelo.CodigoPesable=idCodigo>0 && idCodigo<=99999?CodigoBalanza.Prefijo(idCodigo,false):"";
            modelo.CodigoUnitario=idCodigo>0 && idCodigo<=99999?CodigoBalanza.Prefijo(idCodigo,true):"";
            return View("Formulario",modelo);
        }
    }
}
