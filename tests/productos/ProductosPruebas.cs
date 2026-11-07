using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using Npgsql;
using SistemaVentas.Entidades;
using SistemaVentas.Negocio;
using SistemaVentas.Web.Controllers;
using SistemaVentas.Web.Seguridad;
using SistemaVentas.Web.ViewModels;

internal static class ProductosPruebas
{
    private static int cantidad;
    private static string cadena;
    private static long admin,categoria,unidad;
    private static ServicioProductos productos;
    private static void Comprobar(bool valor,string descripcion)
    { if(!valor) throw new Exception("Falló: "+descripcion);cantidad++;Console.WriteLine("OK: "+descripcion); }
    private static void Denegado(Action accion,string descripcion)
    { bool fallo=false;try{accion();}catch(OperacionNoPermitidaException){fallo=true;}Comprobar(fallo,descripcion); }
    private static object Sql(string sql,params object[] valores)
    {
        using(var c=new NpgsqlConnection(cadena))
        {
            c.Open();using(var cmd=new NpgsqlCommand(sql,c))
            { for(int i=0;i<valores.Length;i+=2) cmd.Parameters.AddWithValue((string)valores[i],valores[i+1]??DBNull.Value);return cmd.ExecuteScalar(); }
        }
    }
    private static SolicitudProducto Nuevo(string codigo="PRUEBA",bool decimales=false)
    { return new SolicitudProducto{Codigo=codigo,Descripcion="Producto de prueba "+codigo,CategoriaId=categoria,UnidadMedidaId=unidad,PermiteDecimales=decimales,PrecioCompra=123.4567m,PrecioVenta=456.78m,StockMinimo=decimales?0.250m:1m}; }
    private static void Entrada(long id,decimal cantidad)
    {
        Sql(@"BEGIN;UPDATE productos SET stock_actual=stock_actual+@cantidad WHERE id=@id;
            INSERT INTO movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,observacion)
            SELECT id,@actor,'AJUSTE_ENTRADA',@cantidad,stock_actual-@cantidad,stock_actual,1.2345,'Entrada de prueba' FROM productos WHERE id=@id;COMMIT;",
            "id",id,"actor",admin,"cantidad",cantidad);
    }
    private static int Main()
    {
        try
        {
            Console.OutputEncoding=System.Text.Encoding.UTF8;
            cadena=Environment.GetEnvironmentVariable("SV_TEST_CONNECTION");productos=new ServicioProductos(cadena);
            var seguridad=new ServicioSeguridad(cadena);
            admin=seguridad.CrearPrimerAdministrador("admin","Administrador",Guid.NewGuid().ToString("N"));
            var roles=seguridad.RolesParaUsuario(admin);
            long cajero=seguridad.GuardarUsuario(admin,new SolicitudUsuario{NombreUsuario="cajero",Nombre="Cajero de prueba",Password=Guid.NewGuid().ToString("N"),RolIds=new[]{roles.Single(x=>x.Codigo=="CAJERO").Id}});
            long supervisor=seguridad.GuardarUsuario(admin,new SolicitudUsuario{NombreUsuario="supervisor",Nombre="Supervisor de prueba",Password=Guid.NewGuid().ToString("N"),RolIds=new[]{roles.Single(x=>x.Codigo=="SUPERVISOR").Id}});
            categoria=(long)Sql("SELECT id FROM categorias WHERE nombre='General'");unidad=(long)Sql("SELECT id FROM unidades_medida WHERE codigo='UN'");
            long kg=(long)Sql("SELECT id FROM unidades_medida WHERE codigo='KG'");
            Comprobar(productos.Listar(cajero).Total==0,"Cajero consulta el listado vacío");
            Denegado(()=>productos.Guardar(cajero,Nuevo()),"Cajero no registra productos por llamada directa");
            Denegado(()=>productos.Listar(0),"Actor inexistente no consulta productos");
            var solicitud=Nuevo("  CAFE-01 ",true);solicitud.Descripcion="  Cafe\u0301 por peso  ";solicitud.UnidadMedidaId=kg;
            solicitud.CodigosBarras=new List<string>{"0012345678901","0012345678902"};
            long id=productos.Guardar(supervisor,solicitud);var p=productos.Obtener(admin,id);
            Comprobar(p.Codigo=="CAFE-01" && p.Descripcion=="Café por peso","Recorte y normalización de código y descripción");
            Comprobar(p.PrecioCompra==123.4567m && p.PrecioVenta==456.78m && p.StockMinimo==.250m && p.StockActual==0,"Precisión decimal exacta y existencia inicial cero");
            Comprobar(p.CodigosBarras.SequenceEqual(solicitud.CodigosBarras),"Varios códigos conservan los ceros iniciales");
            Comprobar(productos.Listar(cajero,new FiltroProductos{Busqueda="café"}).Registros.Single().Id==id,"Búsqueda por descripción Unicode");
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda="cafe-01"}).Registros.Single().Id==id,"Código interno exacto sin distinguir mayúsculas");
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda="0012345678902"}).Registros.Single().Id==id,"Lectura exacta de cualquiera de sus códigos de barras");
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda="' OR 1=1 --"}).Total==0,"Búsqueda parametrizada rechaza inyección");
            var duplicado=Nuevo("cafe-01");Denegado(()=>productos.Guardar(admin,duplicado),"Código interno duplicado rechazado");
            duplicado=Nuevo("OTRO");duplicado.CodigosBarras.Add("0012345678901");Denegado(()=>productos.Guardar(admin,duplicado),"Barra de otro producto rechazada");
            duplicado=Nuevo("0012345678901");Denegado(()=>productos.Guardar(admin,duplicado),"Código interno no invade barra de otro producto");
            duplicado=Nuevo("CRUCE");duplicado.CodigosBarras.Add("cafe-01");Denegado(()=>productos.Guardar(admin,duplicado),"Barra no invade código interno de otro producto");
            duplicado=Nuevo("REPETIDO");duplicado.CodigosBarras.AddRange(new[]{"123","123"});Denegado(()=>productos.Guardar(admin,duplicado),"Barra repetida dentro del formulario rechazada");
            var invalido=Nuevo("LARGO");invalido.Codigo=new string('x',51);Denegado(()=>productos.Guardar(admin,invalido),"Código mayor a 50 rechazado");
            invalido=Nuevo("DESCRIPCION");invalido.Descripcion=" \n ";Denegado(()=>productos.Guardar(admin,invalido),"Descripción vacía rechazada");
            invalido=Nuevo("CONTROL");invalido.CodigosBarras.Add("12\n34");Denegado(()=>productos.Guardar(admin,invalido),"Barra con control rechazada");
            invalido=Nuevo("NEGATIVO");invalido.PrecioCompra=-1;Denegado(()=>productos.Guardar(admin,invalido),"Precio negativo rechazado");
            invalido=Nuevo("REDONDEO");invalido.PrecioVenta=1.001m;Denegado(()=>productos.Guardar(admin,invalido),"Precio de venta no se redondea silenciosamente");
            invalido=Nuevo("ESCALA");invalido.StockMinimo=.1234m;invalido.PermiteDecimales=true;Denegado(()=>productos.Guardar(admin,invalido),"Stock mínimo con más de 3 decimales rechazado");
            invalido=Nuevo("ENTERO");invalido.StockMinimo=.250m;Denegado(()=>productos.Guardar(admin,invalido),"Producto entero rechaza stock mínimo fraccionado");
            invalido=Nuevo("RANGO");invalido.PrecioCompra=100000000000000m;Denegado(()=>productos.Guardar(admin,invalido),"Precio fuera de numeric(18,4) rechazado");
            invalido=Nuevo("FK");invalido.UnidadMedidaId=999999;Denegado(()=>productos.Guardar(admin,invalido),"Unidad inexistente rechazada");
            long inactiva=new ServicioCategorias(cadena).Guardar(admin,0,"Inactiva",false,0);
            invalido=Nuevo("INACTIVA");invalido.CategoriaId=inactiva;Denegado(()=>productos.Guardar(admin,invalido),"Producto activo exige categoría activa");
            long marca=new ServicioMarcas(cadena).Guardar(admin,0,"Marca inactiva",false,0);
            invalido=Nuevo("MARCA");invalido.MarcaId=marca;Denegado(()=>productos.Guardar(admin,invalido),"Producto activo exige marca activa");
            invalido.Activo=false;long archivado=productos.Guardar(admin,invalido);
            Comprobar(!productos.Obtener(admin,archivado).Activo,"Producto inactivo conserva una marca inactiva");
            var opciones=productos.Opciones(admin,inactiva,marca,true);
            Comprobar(opciones.Categorias.Any(x=>x.Id==inactiva) && opciones.Marcas.Any(x=>x.Id==marca) && opciones.Unidades.Count==3,"Formulario conserva referencias inactivas actuales y unidades reales");
            Denegado(()=>productos.Desactivar(cajero,id,p.Version),"Cajero no desactiva productos");
            Denegado(()=>productos.Historial(cajero,id),"Cajero no consulta movimientos sin STOCK_VER");
            Comprobar(productos.Historial(admin,id).Total==0,"Historial vacío sin movimientos simulados");
            long barraId=(long)Sql("SELECT id FROM productos_codigos_barras WHERE codigo='0012345678901'");
            var viejo=productos.Obtener(admin,id);p=productos.Obtener(admin,id);p.CodigosBarras.RemoveAt(1);p.CodigosBarras.Add("0099999999999");
            productos.Guardar(admin,p);p=productos.Obtener(admin,id);
            Comprobar(p.Version!=viejo.Version && !p.CodigosBarras.Contains("0012345678902") && p.CodigosBarras.Contains("0099999999999"),"Cambiar solo barras actualiza la versión y el conjunto");
            Comprobar((long)Sql("SELECT id FROM productos_codigos_barras WHERE codigo='0012345678901'")==barraId,"Barra conservada mantiene su identificador");
            Denegado(()=>productos.Guardar(admin,viejo),"Formulario antiguo no deshace cambios de barras");
            Denegado(()=>productos.Desactivar(admin,id,viejo.Version),"Desactivación con versión antigua rechazada");
            Entrada(id,.250m);p=productos.Obtener(admin,id);
            Comprobar(p.StockActual==.250m && productos.Historial(admin,id).Movimientos.Single().SaldoPosterior==.250m,"Existencia fraccionada e historial leídos de movimientos reales");
            var cambio=productos.Obtener(admin,id);cambio.UnidadMedidaId=unidad;Denegado(()=>productos.Guardar(admin,cambio),"Unidad con movimientos no se reinterpreta");
            cambio=productos.Obtener(admin,id);cambio.PermiteDecimales=false;cambio.StockMinimo=0;Denegado(()=>productos.Guardar(admin,cambio),"Configuración decimal con historial no se cambia");
            p.PrecioVenta=900.50m;p.StockActual=99999;productos.Guardar(admin,p);
            Comprobar(productos.Obtener(admin,id).StockActual==.250m,"Guardar edición ignora StockActual de una entidad manipulada");
            var sinstock=new Producto{Codigo="SIN-STOCK",Descripcion="Ignorar stock enviado",CategoriaId=categoria,UnidadMedidaId=unidad,StockActual=500};
            long sinstockId=productos.Guardar(admin,sinstock);
            Comprobar(productos.Obtener(admin,sinstockId).StockActual==0,"Creación no acepta existencia enviada por el llamador");
            p=productos.Obtener(admin,id);productos.Desactivar(admin,id,p.Version);p=productos.Obtener(admin,id);
            Comprobar(!p.Activo && p.StockActual==.250m && p.CodigosBarras.Count==2,"Desactivar conserva existencias, barras e historial");
            duplicado=Nuevo("cafe-01");Denegado(()=>productos.Guardar(admin,duplicado),"Código de producto inactivo sigue reservado");
            p.Activo=true;productos.Guardar(admin,p);Comprobar(productos.Obtener(admin,id).Activo,"Reactivar conserva identificador");
            var sinHistoria=Nuevo("CAMBIAR-UNIDAD");long sinHistoriaId=productos.Guardar(admin,sinHistoria);
            var editable=productos.Obtener(admin,sinHistoriaId);editable.UnidadMedidaId=kg;editable.PermiteDecimales=true;
            productos.Guardar(admin,editable);
            Comprobar(productos.Obtener(admin,sinHistoriaId).UnidadMedidaId==kg,"Unidad se puede cambiar antes de tener historial");
            Entrada(sinHistoriaId,1m);
            Sql(@"BEGIN;UPDATE productos SET stock_actual=0 WHERE id=@id;
                INSERT INTO movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,observacion)
                VALUES (@id,@actor,'AJUSTE_SALIDA',1,1,0,1,'Salida de prueba');COMMIT;","id",sinHistoriaId,"actor",admin);
            editable=productos.Obtener(admin,sinHistoriaId);editable.UnidadMedidaId=unidad;
            Denegado(()=>productos.Guardar(admin,editable),"Unidad con saldo cero e historial sigue protegida");
            Comprobar(productos.Historial(admin,sinHistoriaId).Total==2 && productos.Obtener(admin,sinHistoriaId).StockActual==0,"Se conserva el historial al agotar el producto");
            // El producto inactivo conserva una categoría que puede desactivarse.
            // La edición de la reactivación y la desactivación de la categoría deben acordarse.
            for(int i=0;i<3;i++)
            {
                var categorias=new ServicioCategorias(cadena);
                long padre=categorias.Guardar(admin,0,"Categoría carrera "+i,true,0);
                var hijo=Nuevo("HIJO-CARRERA-"+i);hijo.CategoriaId=padre;hijo.Activo=false;
                long hijoId=productos.Guardar(admin,hijo);var reactivar=productos.Obtener(admin,hijoId);reactivar.Activo=true;
                long versionPadre=categorias.Obtener(admin,padre).Version;
                Parallel.Invoke(()=>{try{productos.Guardar(admin,reactivar);}catch(OperacionNoPermitidaException){}},
                    ()=>{try{categorias.Desactivar(admin,padre,versionPadre);}catch(OperacionNoPermitidaException){}});
                Comprobar((long)Sql("SELECT count(*) FROM productos p JOIN categorias c ON c.id=p.categoria_id WHERE p.activo AND NOT c.activo")==0,"Edición y desactivación simultánea de categoría sin interbloqueo "+(i+1));
            }
            long marcaActiva=new ServicioMarcas(cadena).Guardar(admin,0,"Marca activa",true,0);
            var filtrable=Nuevo("CON-MARCA");filtrable.MarcaId=marcaActiva;long filtrableId=productos.Guardar(admin,filtrable);
            Comprobar(productos.Listar(admin,new FiltroProductos{CategoriaId=categoria,MarcaId=marcaActiva}).Registros.Single().Id==filtrableId,"Filtros por categoría y marca");
            Comprobar(productos.Listar(admin,new FiltroProductos{Stock="agotado"}).Registros.All(x=>x.StockActual==0),"Filtro agotados");
            Comprobar(productos.Listar(admin,new FiltroProductos{Stock="bajo"}).Registros.All(x=>x.StockActual<=x.StockMinimo),"Filtro de stock bajo incluye el umbral");
            Comprobar(productos.Listar(admin,new FiltroProductos{Estado="inactivos",Busqueda="Producto de prueba MARCA"}).Registros.Single().Id==archivado,"Filtro de productos inactivos");
            for(int i=0;i<23;i++){var n=Nuevo("PAG-"+i.ToString("D2"));n.Descripcion="Paginado "+i.ToString("D2");productos.Guardar(admin,n);}
            var uno=productos.Listar(admin,new FiltroProductos{Busqueda="Paginado"});var dos=productos.Listar(admin,new FiltroProductos{Busqueda="Paginado",Pagina=2});
            Comprobar(uno.Total==23 && uno.Registros.Count==20 && dos.Registros.Count==3,"Paginación SQL de 20 productos");
            Comprobar(!uno.Registros.Select(x=>x.Id).Intersect(dos.Registros.Select(x=>x.Id)).Any(),"Páginas de productos sin filas repetidas");
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda="Paginado",Pagina=999}).Filtro.Pagina==2,"Página superior se ajusta al total");
            for(int i=0;i<24;i++) Entrada(sinstockId,1m);
            Comprobar(productos.Historial(admin,sinstockId).Movimientos.Count==20 && productos.Historial(admin,sinstockId,2).Movimientos.Count==4,"Historial paginado real de 24 movimientos");
            Comprobar(productos.Historial(admin,sinstockId).Producto.StockActual==24,"Historial y existencia consultados en la misma instantánea");
            int exitos=0,conflictos=0;
            var a=productos.Obtener(admin,id);var b=productos.Obtener(admin,id);a.Descripcion="Ventana A";b.Descripcion="Ventana B";
            Parallel.Invoke(()=>{try{productos.Guardar(admin,a);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}},
                ()=>{try{productos.Guardar(admin,b);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}});
            Comprobar(exitos==1 && conflictos==1,"Ediciones simultáneas no pierden cambios");
            exitos=0;conflictos=0;
            Parallel.For(0,2,i=>{try{var n=Nuevo("CARRERA-"+i);n.CodigosBarras.Add("BARRA-CONCURRENTE");productos.Guardar(admin,n);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}});
            Comprobar(exitos==1 && conflictos==1 && (long)Sql("SELECT count(*) FROM productos WHERE codigo LIKE 'CARRERA-%'")==1,"Barra simultánea confirma un solo producto sin inserciones parciales");
            Comprobar((long)Sql("SELECT count(*) FROM auditoria WHERE entidad='productos' AND entidad_id=@id AND accion='PRODUCTO_CREADO'","id",id)==1,"Creación del producto auditada una sola vez");
            long auditoria=(long)Sql("SELECT count(*) FROM auditoria WHERE entidad='productos'");
            p=productos.Obtener(admin,id);p.CodigosBarras.Add("BARRA-CONCURRENTE");p.Descripcion="No guardar";Denegado(()=>productos.Guardar(admin,p),"Edición con barra ajena rechazada");
            Comprobar(productos.Obtener(admin,id).Descripcion!="No guardar" && (long)Sql("SELECT count(*) FROM auditoria WHERE entidad='productos'")==auditoria,"Rechazo conserva datos y auditoría previa");
            Comprobar(productos.Obtener(admin,99999999)==null && productos.Historial(admin,99999999)==null,"Consulta inexistente informa ausencia");
            var modelo=new ProductoFormulario{Codigo="WEB",Descripcion="Desde formulario",CategoriaId=categoria,UnidadMedidaId=unidad,PermiteDecimales=true,PrecioCompra="123,4567",PrecioVenta="456.78",StockMinimo="0,250",CodigosBarras="001\r\n002\n"};
            var estado=new ModelStateDictionary();var solicitudWeb=modelo.Solicitud(estado);
            Comprobar(estado.IsValid && solicitudWeb.PrecioCompra==123.4567m && solicitudWeb.StockMinimo==.250m && solicitudWeb.CodigosBarras.Count==2,"Formulario admite coma y punto sin perder ceros de barras");
            modelo.PrecioVenta="1.234,56";estado=new ModelStateDictionary();modelo.Solicitud(estado);
            Comprobar(!estado.IsValid && estado.ContainsKey("PrecioVenta"),"Formulario rechaza separadores de miles ambiguos");
            var bind=(BindAttribute)typeof(ProductosController).GetMethod("Guardar").GetParameters()[0].GetCustomAttributes(typeof(BindAttribute),false).Single();
            Comprobar(!bind.Include.Contains("StockActual") && !bind.Include.Contains("Opciones") && typeof(SolicitudProducto).GetProperty("StockActual")==null,"El contrato de escritura excluye existencias y opciones");
            foreach(var accion in new[]{"Guardar","Desactivar"})
            {
                var metodo=typeof(ProductosController).GetMethod(accion);
                Comprobar(metodo.IsDefined(typeof(HttpPostAttribute),true) && metodo.IsDefined(typeof(ValidateAntiForgeryTokenAttribute),true),"POST con antifalsificación: "+accion);
            }
            foreach(string accion in new[]{"Crear","Guardar","Desactivar","Movimientos"})
            {
                var contexto=Contexto(accion,seguridad.ObtenerSesion(cajero,1));
                foreach(PermisoAttribute filtro in typeof(ProductosController).GetMethod(accion).GetCustomAttributes(typeof(PermisoAttribute),true)) filtro.OnAuthorization(contexto);
                Comprobar(contexto.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)contexto.Result).StatusCode==403,"Filtro 403 al cajero en "+accion);
            }
            var lectura=Contexto("Index",seguridad.ObtenerSesion(cajero,1));
            foreach(PermisoAttribute filtro in typeof(ProductosController).GetCustomAttributes(typeof(PermisoAttribute),true)) filtro.OnAuthorization(lectura);
            Comprobar(lectura.Result==null,"Filtro admite consulta de productos al cajero");
            Sql("UPDATE roles SET activo=false WHERE codigo='SUPERVISOR'");Denegado(()=>productos.Listar(supervisor),"Permisos revocados se comprueban en la siguiente operación");
            Console.WriteLine("TOTAL: "+cantidad+" comprobaciones de productos correctas.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
    }
    private static AuthorizationContext Contexto(string accion,UsuarioSesion usuario)
    {
        var descriptor=new ReflectedControllerDescriptor(typeof(ProductosController));
        return new AuthorizationContext(new ControllerContext(new ContextoPrueba(usuario),new RouteData(),new ProductosController()),
            new ReflectedActionDescriptor(typeof(ProductosController).GetMethod(accion),accion,descriptor));
    }
    private class ContextoPrueba:HttpContextBase
    {
        private readonly IDictionary items=new Hashtable();private readonly HttpRequestBase request=new SolicitudPrueba();private readonly HttpResponseBase response=new RespuestaPrueba();
        public ContextoPrueba(UsuarioSesion u){items["SistemaVentas.UsuarioActual"]=u;}
        public override IDictionary Items{get{return items;}}public override HttpRequestBase Request{get{return request;}}public override HttpResponseBase Response{get{return response;}}
    }
    private class SolicitudPrueba:HttpRequestBase
    { public override bool IsSecureConnection{get{return true;}}public override string RawUrl{get{return "/Productos";}} }
    private class CachePrueba:HttpCachePolicyBase
    {public override void SetCacheability(HttpCacheability valor){}public override void SetNoStore(){} }
    private class RespuestaPrueba:HttpResponseBase
    {private readonly HttpCachePolicyBase cache=new CachePrueba();public override HttpCachePolicyBase Cache{get{return cache;}}public override bool SuppressFormsAuthenticationRedirect{get;set;} }
}
