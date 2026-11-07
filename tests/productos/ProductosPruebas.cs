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
    { return new SolicitudProducto{Codigo=codigo,Descripcion="Producto de prueba "+codigo,CategoriaId=categoria,UnidadMedidaId=unidad,PermiteDecimales=decimales,PrecioCompra=123.4567m,PrecioVenta=456.78m,StockMinimo=decimales?0.250m:1m,PorcentajeIva=10,GenerarCodigoBarras=true}; }
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
            Comprobar(productos.Listar(cajero).Total==1,"Cajero consulta el producto anterior a la actualización");
            Denegado(()=>productos.Guardar(cajero,Nuevo()),"Cajero no registra productos por llamada directa");
            Denegado(()=>productos.Listar(0),"Actor inexistente no consulta productos");
            var solicitud=Nuevo("  CAFE-01 ",true);solicitud.Descripcion="  Cafe\u0301 por peso  ";solicitud.UnidadMedidaId=kg;
            solicitud.CodigoBarra="0012345678901";solicitud.GenerarCodigoBarras=false;
            long id=productos.Guardar(supervisor,solicitud);var p=productos.Obtener(admin,id);
            Comprobar(p.Codigo=="CAFE-01" && p.Descripcion=="Café por peso","Recorte y normalización de código y descripción");
            Comprobar(p.PrecioCompra==123.4567m && p.PrecioVenta==456.78m && p.StockMinimo==.250m && p.StockActual==0,"Precisión decimal exacta y existencia inicial cero");
            Comprobar(p.CodigoBarra==solicitud.CodigoBarra,"Código único conserva los ceros iniciales");
            Comprobar(productos.Listar(cajero,new FiltroProductos{Busqueda="café"}).Registros.Single().Id==id,"Búsqueda por descripción Unicode");
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda="cafe-01"}).Registros.Single().Id==id,"Código interno exacto sin distinguir mayúsculas");
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda="0012345678901"}).Registros.Single().Id==id,"Lectura exacta de su código de barras");
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda="' OR 1=1 --"}).Total==0,"Búsqueda parametrizada rechaza inyección");
            var duplicado=Nuevo("cafe-01");Denegado(()=>productos.Guardar(admin,duplicado),"Código interno duplicado rechazado");
            duplicado=Nuevo("OTRO");duplicado.CodigoBarra="0012345678901";duplicado.GenerarCodigoBarras=false;Denegado(()=>productos.Guardar(admin,duplicado),"Barra de otro producto rechazada");
            duplicado=Nuevo("0012345678901");Denegado(()=>productos.Guardar(admin,duplicado),"Código interno no invade barra de otro producto");
            duplicado=Nuevo("CRUCE");duplicado.CodigoBarra="cafe-01";duplicado.GenerarCodigoBarras=false;Denegado(()=>productos.Guardar(admin,duplicado),"Barra no invade código interno de otro producto");
            duplicado=Nuevo("REPETIDO");duplicado.CodigoBarra="123\n456";duplicado.GenerarCodigoBarras=false;Denegado(()=>productos.Guardar(admin,duplicado),"No se admiten dos barras separadas por líneas");
            var invalido=Nuevo("LARGO");invalido.Codigo=new string('x',51);Denegado(()=>productos.Guardar(admin,invalido),"Código mayor a 50 rechazado");
            invalido=Nuevo("DESCRIPCION");invalido.Descripcion=" \n ";Denegado(()=>productos.Guardar(admin,invalido),"Descripción vacía rechazada");
            invalido=Nuevo("CONTROL");invalido.CodigoBarra="12\n34";invalido.GenerarCodigoBarras=false;Denegado(()=>productos.Guardar(admin,invalido),"Barra con control rechazada");
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
            var viejo=productos.Obtener(admin,id);p=productos.Obtener(admin,id);p.CodigoBarra="0099999999999";
            productos.Guardar(admin,p);p=productos.Obtener(admin,id);
            Comprobar(p.Version!=viejo.Version && p.CodigoBarra=="0099999999999","Cambiar la única barra actualiza la versión");
            Comprobar((long)Sql("SELECT id FROM productos_codigos_barras WHERE codigo='0099999999999'")==barraId,"Barra conservada mantiene su identificador");
            Denegado(()=>productos.Guardar(admin,viejo),"Formulario antiguo no deshace cambios de barras");
            Denegado(()=>productos.Desactivar(admin,id,viejo.Version),"Desactivación con versión antigua rechazada");
            Entrada(id,.250m);p=productos.Obtener(admin,id);
            Comprobar(p.StockActual==.250m && productos.Historial(admin,id).Movimientos.Single().SaldoPosterior==.250m,"Existencia fraccionada e historial leídos de movimientos reales");
            var cambio=productos.Obtener(admin,id);cambio.UnidadMedidaId=unidad;Denegado(()=>productos.Guardar(admin,cambio),"Unidad con movimientos no se reinterpreta");
            cambio=productos.Obtener(admin,id);cambio.PermiteDecimales=false;cambio.StockMinimo=0;Denegado(()=>productos.Guardar(admin,cambio),"Configuración decimal con historial no se cambia");
            p.PrecioVenta=900.50m;p.StockActual=99999;productos.Guardar(admin,p);
            Comprobar(productos.Obtener(admin,id).StockActual==.250m,"Guardar edición ignora StockActual de una entidad manipulada");
            var sinstock=new Producto{Codigo="SIN-STOCK",Descripcion="Ignorar stock enviado",CategoriaId=categoria,UnidadMedidaId=unidad,StockActual=500,PorcentajeIva=0,GenerarCodigoBarras=true};
            long sinstockId=productos.Guardar(admin,sinstock);
            Comprobar(productos.Obtener(admin,sinstockId).StockActual==0,"Creación no acepta existencia enviada por el llamador");
            p=productos.Obtener(admin,id);productos.Desactivar(admin,id,p.Version);p=productos.Obtener(admin,id);
            Comprobar(!p.Activo && p.StockActual==.250m && p.CodigoBarra=="0099999999999","Desactivar conserva existencias, barras e historial");
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
            Parallel.For(0,2,i=>{try{var n=Nuevo("CARRERA-"+i);n.CodigoBarra="BARRA-CONCURRENTE";n.GenerarCodigoBarras=false;productos.Guardar(admin,n);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}});
            Comprobar(exitos==1 && conflictos==1 && (long)Sql("SELECT count(*) FROM productos WHERE codigo LIKE 'CARRERA-%'")==1,"Barra simultánea confirma un solo producto sin inserciones parciales");
            Comprobar((long)Sql("SELECT count(*) FROM auditoria WHERE entidad='productos' AND entidad_id=@id AND accion='PRODUCTO_CREADO'","id",id)==1,"Creación del producto auditada una sola vez");
            long auditoria=(long)Sql("SELECT count(*) FROM auditoria WHERE entidad='productos'");
            p=productos.Obtener(admin,id);p.CodigoBarra="BARRA-CONCURRENTE";p.GenerarCodigoBarras=false;p.Descripcion="No guardar";Denegado(()=>productos.Guardar(admin,p),"Edición con barra ajena rechazada");
            Comprobar(productos.Obtener(admin,id).Descripcion!="No guardar" && (long)Sql("SELECT count(*) FROM auditoria WHERE entidad='productos'")==auditoria,"Rechazo conserva datos y auditoría previa");
            Comprobar(productos.Obtener(admin,99999999)==null && productos.Historial(admin,99999999)==null,"Consulta inexistente informa ausencia");
            var modelo=new ProductoFormulario{Codigo="WEB",Descripcion="Desde formulario",CategoriaId=categoria,UnidadMedidaId=unidad,PermiteDecimales=true,PrecioCompra="123,4567",PrecioVenta="456.78",StockMinimo="0,250",CodigoBarra="001",PorcentajeIva=5};
            var estado=new ModelStateDictionary();var solicitudWeb=modelo.Solicitud(estado);
            Comprobar(estado.IsValid && solicitudWeb.PrecioCompra==123.4567m && solicitudWeb.StockMinimo==.250m && solicitudWeb.CodigoBarra=="001","Formulario admite coma y punto sin perder ceros de barras");
            modelo.PrecioVenta="1.234,56";estado=new ModelStateDictionary();modelo.Solicitud(estado);
            Comprobar(!estado.IsValid && estado.ContainsKey("PrecioVenta"),"Formulario rechaza separadores de miles ambiguos");
            var bind=(BindAttribute)typeof(ProductosController).GetMethod("Guardar").GetParameters()[0].GetCustomAttributes(typeof(BindAttribute),false).Single();
            Comprobar(!bind.Include.Contains("StockActual") && !bind.Include.Contains("Opciones") && typeof(SolicitudProducto).GetProperty("StockActual")==null,"El contrato de escritura excluye existencias y opciones");
            foreach(var accion in new[]{"Nuevo","Guardar","Desactivar"})
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
            Comprobar(Ean13.Generar(26)=="2800000000264" && Ean13.Generar(36)=="2800000000363","Ejemplos EAN-13 de los productos 26 y 36");
            Comprobar(Ean13.EsValido("4006381333931") && Ean13.EsValido(Ean13.Generar(36)) && !Ean13.EsValido("2800000000364"),"Control EAN-13 estándar y rechazo de dígito alterado");
            Denegado(()=>Ean13.Generar(0),"Generación rechaza id cero");
            Denegado(()=>Ean13.Generar(10000000000L),"Generación no trunca un id de más de 10 dígitos");
            Comprobar(Ean13.Generar(9999999999L).Length==13,"Último id admitido genera exactamente 13 dígitos");
            using(var c=new NpgsqlConnection(cadena))
            {
                c.Open();using(var cmd=new NpgsqlCommand("SELECT n,sv_ean13_producto(n) FROM generate_series(1,1000) n",c))
                using(var reader=cmd.ExecuteReader()) while(reader.Read())
                    if(reader.GetString(1)!=Ean13.Generar(reader.GetInt32(0))) throw new Exception("SQL y C# difieren en EAN-13");
            }
            Comprobar(true,"SQL y C# coinciden en 1000 identificadores EAN-13");
            var legacyId=(long)Sql("SELECT id FROM productos WHERE codigo='LEGADO'");
            var legacy=productos.Obtener(admin,legacyId);
            Comprobar(legacy.PorcentajeIva==null && legacy.CodigoBarra=="LEGADO-BARRA" && legacy.PrecioVenta==123.45m,"Migración conserva barra, precio e IVA pendiente de producto anterior");
            Denegado(()=>productos.Guardar(admin,legacy),"Producto anterior requiere seleccionar su IVA al editar");
            legacy.PorcentajeIva=5;productos.Guardar(admin,legacy);legacy=productos.Obtener(admin,legacyId);
            legacy.PorcentajeIva=10;productos.Guardar(admin,legacy);
            Comprobar(productos.Obtener(admin,legacyId).PrecioVenta==123.45m && productos.Obtener(admin,legacyId).PrecioCompra==90m,"Cambiar IVA conserva ambos precios que ya incluyen el impuesto");
            var sinIva=Nuevo("IVA-NULO");sinIva.PorcentajeIva=null;Denegado(()=>productos.Guardar(admin,sinIva),"No se asigna IVA implícito a un producto nuevo");
            sinIva.PorcentajeIva=7;Denegado(()=>productos.Guardar(admin,sinIva),"IVA fuera de 0,5,10 rechazado");
            Denegado(()=>productos.ReservarNuevo(cajero),"Cajero no reserva números de productos");
            long antes=(long)Sql("SELECT count(*) FROM productos");
            var reservas=new long[8];Parallel.For(0,8,i=>reservas[i]=productos.ReservarNuevo(admin));
            Comprobar(reservas.Distinct().Count()==8 && (long)Sql("SELECT count(*) FROM productos")==antes,"Reservas simultáneas distintas sin crear productos vacíos");
            var automatico=Nuevo("AUTO-RESERVADO");automatico.IdNuevo=reservas[0];automatico.CodigoBarra="NO-CONFIAR";
            long automaticoId=productos.Guardar(admin,automatico);var generado=productos.Obtener(admin,automaticoId);
            Comprobar(automaticoId==reservas[0] && generado.CodigoBarra==Ean13.Generar(automaticoId) && generado.GenerarCodigoBarras,"Guardar conserva el número reservado y recalcula su EAN-13");
            Denegado(()=>productos.Guardar(admin,automatico),"Reenviar el formulario no duplica el producto ni lo sobrescribe");
            long automatico2=productos.Guardar(admin,new SolicitudProducto{IdNuevo=reservas[7],Codigo="OTRA-RESERVA",Descripcion="Segunda reserva",CategoriaId=categoria,UnidadMedidaId=unidad,PorcentajeIva=0,GenerarCodigoBarras=true});
            Comprobar(automatico2==reservas[7] && productos.Obtener(admin,automatico2).CodigoBarra==Ean13.Generar(automatico2),"Guardar fuera del orden de reserva mantiene cada id y barra");
            bool segundaRechazada=false;
            try{Sql("INSERT INTO productos_codigos_barras(producto_id,codigo) VALUES (@id,'SEGUNDA-BARRA')","id",automaticoId);}catch(PostgresException e)when(e.SqlState=="23505"){segundaRechazada=true;}
            Comprobar(segundaRechazada,"PostgreSQL impide una segunda barra incluso mediante SQL directo");
            bool incorrecta=false;
            try{Sql("UPDATE productos_codigos_barras SET codigo='BARRA-FALSA' WHERE producto_id=@id","id",automaticoId);}catch(PostgresException e)when(e.SqlState=="23503"){incorrecta=true;}
            Comprobar(incorrecta && productos.Obtener(admin,automaticoId).CodigoBarra==Ean13.Generar(automaticoId),"PostgreSQL revierte una barra generada que no coincide con su id");
            bool ivaRechazado=false;
            try{Sql("UPDATE productos SET porcentaje_iva=7 WHERE id=@id","id",automaticoId);}catch(PostgresException e)when(e.SqlState=="23514"){ivaRechazado=true;}
            Comprobar(ivaRechazado,"PostgreSQL rechaza porcentajes no admitidos");
            var token=ReservaNuevoProducto.Crear(reservas[1],admin);
            Comprobar(ReservaNuevoProducto.Leer(token,admin)==reservas[1],"Reserva protegida conserva su id");
            Denegado(()=>ReservaNuevoProducto.Leer(token,cajero),"La reserva no se puede copiar a otro usuario");
            Denegado(()=>ReservaNuevoProducto.Leer("alterado",admin),"Reserva alterada rechazada");
            Comprobar(!bind.Include.Contains("IdNuevo"),"El id reservado no se acepta como un campo modificable por el navegador");
            Comprobar(CodigoBalanza.Prefijo(3,false)=="2000003" && CodigoBalanza.Prefijo(5,true)=="2100005","Ejemplos de bandera y PLU de siete dígitos");
            Comprobar(CodigoBalanza.Etiqueta(3,false,250)=="2000003002504" && CodigoBalanza.Etiqueta(5,true,3)=="2100005000031","Etiquetas completas: bandera 2 + PLU 5 + valor 5 + control 1");
            var lecturaPeso=CodigoBalanza.Leer("2000003002504");var lecturaUnidad=CodigoBalanza.Leer("2100005000031");
            Comprobar(lecturaPeso.Plu==3 && !lecturaPeso.EsUnitario && lecturaPeso.ValorCodificado=="00250" && lecturaUnidad.Plu==5 && lecturaUnidad.EsUnitario && lecturaUnidad.ValorCodificado=="00003","Lectura conserva los cinco dígitos variables sin cambiar la identificación fija");
            Comprobar(CodigoBalanza.Leer("2000003002505")==null && CodigoBalanza.Leer("2000003")==null && CodigoBalanza.Leer(Ean13.Generar(36))==null,"Solo interpreta etiquetas completas con bandera y control válidos");
            Denegado(()=>CodigoBalanza.Prefijo(100000,false),"PLU mayor a cinco dígitos no se trunca");
            Denegado(()=>CodigoBalanza.Prefijo(0,true),"PLU cero rechazado");
            Denegado(()=>CodigoBalanza.Etiqueta(3,false,100000),"Peso o cantidad no desborda los cinco dígitos");
            Comprobar(CodigoBalanza.Prefijo(99999,true)=="2199999","Último PLU admitido conserva cinco dígitos");
            var pesable=Nuevo("BALANZA-PESO");pesable.GenerarCodigoBarras=false;pesable.BalanzaPesable=true;pesable.PermiteDecimales=true;pesable.CodigoBarra="MANIPULADO";
            long pesoId=productos.Guardar(admin,pesable);var pesoGuardado=productos.Obtener(admin,pesoId);
            Comprobar(pesoGuardado.BalanzaPesable && !pesoGuardado.BalanzaUnitario && !pesoGuardado.GenerarCodigoBarras && pesoGuardado.CodigoBarra==CodigoBalanza.Prefijo(pesoId,false),"Guarda pesable y recalcula el prefijo a partir del ID real");
            var unitario=Nuevo("BALANZA-UNIDAD");unitario.GenerarCodigoBarras=false;unitario.BalanzaUnitario=true;
            long unidadId=productos.Guardar(admin,unitario);var unidadGuardada=productos.Obtener(admin,unidadId);
            Comprobar(unidadGuardada.BalanzaUnitario && !unidadGuardada.BalanzaPesable && unidadGuardada.CodigoBarra==CodigoBalanza.Prefijo(unidadId,true),"Guarda tipo unitario y código de bandera 21");
            string etiquetaPeso=CodigoBalanza.Etiqueta(pesoId,false,250);string etiquetaUnidad=CodigoBalanza.Etiqueta(unidadId,true,3);
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda=etiquetaPeso}).Registros.Single().Id==pesoId,"Busca pesable con una etiqueta completa de peso variable");
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda=etiquetaUnidad}).Registros.Single().Id==unidadId,"Busca unitario con una etiqueta completa de cantidad variable");
            Comprobar(productos.Listar(admin,new FiltroProductos{Busqueda=CodigoBalanza.Prefijo(pesoId,false)}).Registros.Single().Id==pesoId,"También busca la identificación fija de siete dígitos");
            var incompatible=Nuevo("DOS-TIPOS");incompatible.BalanzaPesable=true;Denegado(()=>productos.Guardar(admin,incompatible),"EAN-13 y balanza a la vez rechazados");
            incompatible.GenerarCodigoBarras=false;incompatible.BalanzaUnitario=true;Denegado(()=>productos.Guardar(admin,incompatible),"Pesable y unitario a la vez rechazados");
            bool tiposRechazados=false;
            try{Sql("UPDATE productos SET balanza_unitario=true WHERE id=@id","id",pesoId);}catch(PostgresException e)when(e.SqlState=="23514"){tiposRechazados=true;}
            Comprobar(tiposRechazados,"PostgreSQL también impide marcar tipos incompatibles");
            bool barraRechazada=false;
            try{Sql("UPDATE productos_codigos_barras SET codigo='2000000' WHERE producto_id=@id","id",pesoId);}catch(PostgresException e)when(e.SqlState=="23503"){barraRechazada=true;}
            Comprobar(barraRechazada,"La base rechaza un prefijo de balanza que no corresponde al ID");
            var editarBalanza=ProductoFormulario.Desde(pesoGuardado);
            Comprobar(editarBalanza.BalanzaPesable && !editarBalanza.BalanzaUnitario && !editarBalanza.GenerarCodigoBarras,"Editar restaura las casillas de balanza persistidas");
            pesoGuardado.BalanzaPesable=false;pesoGuardado.GenerarCodigoBarras=true;productos.Guardar(admin,pesoGuardado);
            var convertido=productos.Obtener(admin,pesoId);
            Comprobar(convertido.CodigoBarra==Ean13.Generar(pesoId) && !convertido.BalanzaPesable && convertido.PorcentajeIva==10 && convertido.StockActual==0,"Cambiar formato conserva IVA, precios y stock");
            using(var c=new NpgsqlConnection(cadena))
            {
                c.Open();using(var cmd=new NpgsqlCommand("SELECT n,sv_codigo_balanza(n,false),sv_codigo_balanza(n,true) FROM generate_series(1,1000) n",c))
                using(var reader=cmd.ExecuteReader()) while(reader.Read())
                    if(reader.GetString(1)!=CodigoBalanza.Prefijo(reader.GetInt32(0),false) || reader.GetString(2)!=CodigoBalanza.Prefijo(reader.GetInt32(0),true)) throw new Exception("SQL y C# difieren en balanza");
            }
            Comprobar(true,"SQL y C# coinciden para 1000 PLU en ambos modos de balanza");
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
