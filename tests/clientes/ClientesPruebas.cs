using System;
using System.Collections;
using System.Collections.Generic;
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

internal static class ClientesPruebas
{
    private static int cantidad;
    private static string cadena;
    private static long admin,producto;
    private static ServicioClientes clientes;
    private static readonly Dictionary<long,long> sesiones=new Dictionary<long,long>();
    private static void Comprobar(bool valor,string descripcion)
    {if(!valor)throw new Exception("Falló: "+descripcion);cantidad++;Console.WriteLine("OK: "+descripcion);}
    private static void Denegado(Action accion,string descripcion)
    {bool rechazo=false;try{accion();}catch(OperacionNoPermitidaException){rechazo=true;}Comprobar(rechazo,descripcion);}
    private static object Ejecutar(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] valores)
    {
        using(var cmd=new NpgsqlCommand(sql,c,t))
        {for(int i=0;i<valores.Length;i+=2)cmd.Parameters.AddWithValue((string)valores[i],valores[i+1]??DBNull.Value);return cmd.ExecuteScalar();}
    }
    private static object Sql(string sql,params object[] valores)
    {using(var c=new NpgsqlConnection(cadena)){c.Open();return Ejecutar(c,null,sql,valores);}}
    private static SolicitudCliente Nuevo(string nombre="Cliente de prueba")
    {return new SolicitudCliente{Nombre=nombre,TipoDocumento="CI",Documento=Guid.NewGuid().ToString("N"),Telefono="+595 981 123456",Direccion="Dirección de prueba",Correo="cliente@example.com"};}
    // Preparación exclusiva de la base descartable: ventas completas con pagos,
    // stock coherente y auditoría. No es un servicio POS ni toca la base del usuario.
    private static long Sesion(long actor)
    {
        long id;if(sesiones.TryGetValue(actor,out id))return id;
        long caja=(long)Sql("INSERT INTO cajas(codigo,nombre) VALUES (@codigo,'Caja temporal') RETURNING id","codigo","TEST_"+actor);
        id=(long)Sql("INSERT INTO cajas_sesiones(caja_id,usuario_apertura_id,monto_inicial) VALUES (@caja,@actor,0) RETURNING id","caja",caja,"actor",actor);
        sesiones.Add(actor,id);return id;
    }
    private static long Venta(long actor,long cliente,bool confirmar=true)
    {
        long sesion=Sesion(actor);
        using(var c=new NpgsqlConnection(cadena))
        {
            c.Open();using(var t=c.BeginTransaction())
            {
                long venta=(long)Ejecutar(c,t,@"INSERT INTO ventas(clave_idempotencia,solicitud_hash,cliente_id,usuario_id,caja_sesion_id,subtotal,total)
                    VALUES (@clave,repeat('a',64),@cliente,@actor,@sesion,10,10) RETURNING id","clave",Guid.NewGuid(),"cliente",cliente,"actor",actor,"sesion",sesion);
                if(confirmar)
                {
                    long detalle=(long)Ejecutar(c,t,@"INSERT INTO ventas_detalles(venta_id,producto_id,codigo_producto,descripcion_producto,unidad_medida,cantidad,precio_unitario,subtotal,costo_unitario)
                        VALUES (@venta,@producto,'TEST-HIST','Producto temporal','UN',1,10,10,1) RETURNING id","venta",venta,"producto",producto);
                    Ejecutar(c,t,"UPDATE productos SET stock_actual=stock_actual-1 WHERE id=@id","id",producto);
                    Ejecutar(c,t,@"INSERT INTO movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,venta_detalle_id)
                        SELECT id,@actor,'SALIDA_VENTA',1,stock_actual+1,stock_actual,1,@detalle FROM productos WHERE id=@id","actor",actor,"detalle",detalle,"id",producto);
                    Ejecutar(c,t,@"INSERT INTO pagos(venta_id,metodo_pago_id,monto,es_efectivo)
                        VALUES (@venta,(SELECT id FROM metodos_pago WHERE codigo='TARJETA'),10,false)","venta",venta);
                    Ejecutar(c,t,"INSERT INTO auditoria(usuario_id,accion,entidad,entidad_id) VALUES (@actor,'VENTA_CONFIRMADA','ventas',@venta)","actor",actor,"venta",venta);
                    Ejecutar(c,t,"UPDATE ventas SET estado='CONFIRMADA' WHERE id=@venta","venta",venta);
                }
                t.Commit();return venta;
            }
        }
    }
    private static int Main()
    {
        try
        {
            Console.OutputEncoding=System.Text.Encoding.UTF8;
            cadena=Environment.GetEnvironmentVariable("SV_TEST_CONNECTION");clientes=new ServicioClientes(cadena);
            var seguridad=new ServicioSeguridad(cadena);
            admin=seguridad.CrearPrimerAdministrador("admin","Administrador",Guid.NewGuid().ToString("N"));
            var roles=seguridad.RolesParaUsuario(admin);long rolCajero=roles.Single(r=>r.Codigo=="CAJERO").Id;
            long cajero=seguridad.GuardarUsuario(admin,new SolicitudUsuario{NombreUsuario="cajero",Nombre="Cajero uno",Password=Guid.NewGuid().ToString("N"),RolIds=new[]{rolCajero}});
            long otro=seguridad.GuardarUsuario(admin,new SolicitudUsuario{NombreUsuario="otro",Nombre="Cajero dos",Password=Guid.NewGuid().ToString("N"),RolIds=new[]{rolCajero}});
            var inicial=clientes.Listar(admin);var generico=inicial.Registros.Single();
            Comprobar(inicial.Total==1 && generico.EsGenerico && generico.Activo && generico.Nombre=="Consumidor final","Cliente genérico existente conservado y visible");
            Denegado(()=>clientes.Listar(0),"Actor inexistente no consulta clientes");
            var alta=Nuevo("  Jose\u0301 Valenzuela  ");alta.TipoDocumento=" ruc ";alta.Documento=" 80001234-5 ";
            long id=clientes.Guardar(cajero,alta);var c=clientes.Obtener(admin,id);
            Comprobar(c.Nombre=="José Valenzuela" && c.TipoDocumento=="RUC" && c.Documento=="80001234-5" && !c.EsGenerico,"Cajero registra cliente normalizado sin alterar documento");
            Comprobar(c.Telefono==alta.Telefono && c.Direccion==alta.Direccion && c.Correo==alta.Correo,"Contacto se guarda en PostgreSQL");
            Denegado(()=>clientes.Guardar(cajero,c),"Cajero no edita por llamada directa");
            Denegado(()=>clientes.Desactivar(cajero,id,c.Version),"Cajero no desactiva por llamada directa");
            var inactivo=Nuevo("Inactivo de prueba");inactivo.Activo=false;Denegado(()=>clientes.Guardar(cajero,inactivo),"Cajero no elude el permiso de edición creando inactivos");
            long inactivoId=clientes.Guardar(admin,inactivo);Comprobar(!clientes.Obtener(admin,inactivoId).Activo,"Administrador puede registrar cliente inactivo");
            var repetido=Nuevo();repetido.TipoDocumento="ruc";repetido.Documento="80001234-5";
            Denegado(()=>clientes.Guardar(admin,repetido),"Duplicado de tipo y documento rechazado sin distinguir mayúsculas");
            var variante=Nuevo();variante.TipoDocumento="CI";variante.Documento="80001234-5";
            long varianteId=clientes.Guardar(admin,variante);Comprobar(varianteId!=id,"Documentos iguales de tipos distintos son registros diferentes");
            var ceros=Nuevo("Con ceros");ceros.Documento="0000123";long cerosId=clientes.Guardar(admin,ceros);
            Comprobar(clientes.Obtener(admin,cerosId).Documento=="0000123","Documento conserva ceros iniciales");
            var sinDocumento=Nuevo("Sin documento");sinDocumento.TipoDocumento=null;sinDocumento.Documento=null;sinDocumento.Correo=" ";sinDocumento.Telefono=" ";sinDocumento.Direccion=" ";
            long sinDocId=clientes.Guardar(admin,sinDocumento);var vacios=clientes.Obtener(admin,sinDocId);
            Comprobar(vacios.Documento==null && vacios.TipoDocumento==null && vacios.Correo==null && vacios.Telefono==null && vacios.Direccion==null,"Opcionales vacíos se guardan como NULL sin documento ficticio");
            var invalido=Nuevo();invalido.TipoDocumento=null;Denegado(()=>clientes.Guardar(admin,invalido),"Número de documento exige tipo");
            invalido=Nuevo();invalido.Documento=null;Denegado(()=>clientes.Guardar(admin,invalido),"Tipo de documento exige número");
            invalido=Nuevo(" ");Denegado(()=>clientes.Guardar(admin,invalido),"Nombre vacío rechazado");
            invalido=Nuevo(new string('a',151));Denegado(()=>clientes.Guardar(admin,invalido),"Nombre mayor a 150 rechazado");
            invalido=Nuevo("Salto\nde línea");Denegado(()=>clientes.Guardar(admin,invalido),"Nombre con control rechazado");
            invalido=Nuevo();invalido.Correo="sin-arroba";Denegado(()=>clientes.Guardar(admin,invalido),"Correo mal formado rechazado");
            invalido=Nuevo();invalido.Correo="Nombre <cliente@example.com>";Denegado(()=>clientes.Guardar(admin,invalido),"Correo con nombre adicional rechazado");
            invalido=Nuevo();invalido.Telefono=new string('1',41);Denegado(()=>clientes.Guardar(admin,invalido),"Teléfono mayor a 40 rechazado");
            invalido=Nuevo();invalido.TipoDocumento="INVALIDO";Denegado(()=>clientes.Guardar(admin,invalido),"Tipo desconocido rechazado");
            long auditados=(long)Sql("SELECT count(*) FROM auditoria WHERE entidad='clientes'");
            var viejo=clientes.Obtener(admin,id);c=clientes.Obtener(admin,id);c.Nombre="José editado";clientes.Guardar(admin,c);
            var editado=clientes.Obtener(admin,id);Comprobar(editado.Nombre=="José editado" && editado.Version!=viejo.Version,"Edición cambia el nombre y la versión");
            Denegado(()=>clientes.Guardar(admin,viejo),"Formulario antiguo no sobrescribe cambios");
            Denegado(()=>clientes.Desactivar(admin,id,viejo.Version),"Desactivación con versión antigua rechazada");
            Comprobar((long)Sql("SELECT count(*) FROM auditoria WHERE entidad='clientes'")==auditados+1,"Rechazos no generan auditoría de éxito");
            clientes.Desactivar(admin,id,editado.Version);c=clientes.Obtener(admin,id);
            Comprobar(!c.Activo && c.Documento=="80001234-5","Desactivar conserva datos e identificador");
            Denegado(()=>clientes.Guardar(admin,repetido),"Documento inactivo sigue reservado");
            c.Activo=true;clientes.Guardar(admin,c);Comprobar(clientes.Obtener(admin,id).Activo,"Reactivar conserva registro");
            long antesGenerico=(long)Sql("SELECT count(*) FROM auditoria WHERE entidad='clientes'");
            generico.Nombre="No cambiar";Denegado(()=>clientes.Guardar(admin,generico),"Consumidor final no puede renombrarse desde el servicio");
            Denegado(()=>clientes.Desactivar(admin,generico.Id,generico.Version),"Consumidor final no se desactiva");
            Comprobar(clientes.Obtener(admin,generico.Id).EsGenerico && clientes.Obtener(admin,generico.Id).Nombre=="Consumidor final" && (long)Sql("SELECT count(*) FROM auditoria WHERE entidad='clientes'")==antesGenerico,"Genérico intacto sin escrituras ni auditoría parcial");
            var falsoGenerico=new Cliente{Nombre="No genérico",EsGenerico=true};long falsoId=clientes.Guardar(admin,falsoGenerico);
            Comprobar(!clientes.Obtener(admin,falsoId).EsGenerico,"El contrato de escritura ignora un indicador genérico manipulado");
            Comprobar(clientes.Listar(admin,1,"editado").Registros.Single().Id==id && clientes.Listar(admin,1,"80001234").Registros.Count==2,"Búsqueda por nombre y documento o RUC");
            Comprobar(clientes.Listar(admin,1,"' OR 1=1 --").Total==0,"Búsqueda parametrizada no ejecuta instrucciones recibidas");
            Comprobar(clientes.Listar(admin,1,"","inactivos").Registros.Single().Id==inactivoId,"Filtro de clientes inactivos");
            for(int i=0;i<24;i++)clientes.Guardar(admin,Nuevo("Página "+i.ToString("D2")));
            var primera=clientes.Listar(admin,1,"Página");var segunda=clientes.Listar(admin,2,"Página");
            Comprobar(primera.Total==24 && primera.Registros.Count==20 && segunda.Registros.Count==4,"Paginación SQL de 20 clientes");
            Comprobar(!primera.Registros.Select(x=>x.Id).Intersect(segunda.Registros.Select(x=>x.Id)).Any(),"Orden de páginas sin registros repetidos");
            Comprobar(clientes.Listar(admin,999,"Página").Pagina==2 && clientes.Listar(admin,-2,"Página").Pagina==1,"Límites de página ajustados");
            int exitos=0,conflictos=0;var a=clientes.Obtener(admin,id);var b=clientes.Obtener(admin,id);a.Nombre="Ventana A";b.Nombre="Ventana B";
            Parallel.Invoke(()=>{try{clientes.Guardar(admin,a);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}},
                ()=>{try{clientes.Guardar(admin,b);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}});
            Comprobar(exitos==1 && conflictos==1,"Dos ediciones simultáneas confirman un solo cambio");
            exitos=0;conflictos=0;
            Parallel.For(0,2,i=>{var p=Nuevo("Duplicado concurrente");p.Documento="CONCURRENTE";try{clientes.Guardar(admin,p);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}});
            Comprobar(exitos==1 && conflictos==1 && (long)Sql("SELECT count(*) FROM clientes WHERE documento='CONCURRENTE'")==1,"Unicidad concurrente no crea clientes duplicados");
            Comprobar(clientes.Historial(admin,id).Total==0 && clientes.Historial(cajero,id).SoloPropias,"Historial inicial vacío y alcance propio del cajero");
            producto=(long)Sql(@"INSERT INTO productos(codigo,descripcion,categoria_id,unidad_medida_id)
                SELECT 'TEST-HIST','Producto temporal',c.id,u.id FROM categorias c CROSS JOIN unidades_medida u WHERE c.nombre='General' AND u.codigo='UN' RETURNING id");
            Sql(@"BEGIN;UPDATE productos SET stock_actual=100 WHERE id=@id;
                INSERT INTO movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario)
                VALUES (@id,@actor,'SALDO_INICIAL',100,0,100,1);COMMIT;","id",producto,"actor",admin);
            for(int i=0;i<25;i++)Venta(admin,id);
            Venta(cajero,id);Venta(otro,id);Venta(cajero,id,false);
            var historial=clientes.Historial(admin,id);var propias=clientes.Historial(cajero,id);
            Comprobar(historial.Total==27 && historial.ImporteTotal==270m && historial.Compras.Count==20 && !historial.SoloPropias,"Administrador ve ventas confirmadas reales y total coherente");
            Comprobar(clientes.Historial(admin,id,2).Compras.Count==7 && clientes.Historial(admin,id,999).Pagina==2,"Historial paginado de ventas confirmadas");
            Comprobar(propias.Total==1 && propias.ImporteTotal==10m && propias.Compras.Single().Vendedor=="Cajero uno","Cajero solo ve su venta y su importe, sin filtrar otras cuentas");
            Comprobar(clientes.Historial(otro,id).Total==1 && clientes.Historial(admin,sinDocId).Total==0,"Historial respeta actor y cliente");
            c=clientes.Obtener(admin,id);clientes.Desactivar(admin,id,c.Version);
            Comprobar(clientes.Historial(admin,id).Total==27 && (long)Sql("SELECT count(*) FROM ventas WHERE cliente_id=@id","id",id)==28,"Desactivación conserva documentos e historial, incluidos borradores");
            long soloLectura=seguridad.GuardarRol(admin,new SolicitudRol{Codigo="LECTURA_CLIENTES",Nombre="Lectura de clientes",PermisoIds=new[]{seguridad.ListarPermisos(admin).Single(x=>x.Codigo=="CLIENTES_VER").Id}});
            var usuario=seguridad.ObtenerUsuario(admin,otro);seguridad.GuardarUsuario(admin,new SolicitudUsuario{Id=otro,NombreUsuario=usuario.NombreUsuario,Nombre=usuario.Nombre,Activo=true,RolIds=new[]{soloLectura}});
            Denegado(()=>clientes.Historial(otro,id),"CLIENTES_VER sin permisos de ventas no revela historial");
            Denegado(()=>clientes.Guardar(otro,Nuevo()),"Rol de solo lectura no registra clientes");
            Comprobar(clientes.Obtener(otro,id)!=null,"Rol de solo lectura mantiene consulta del cliente");
            Comprobar(clientes.Obtener(admin,99999999)==null && clientes.Historial(admin,99999999)==null,"Cliente inexistente devuelve ausencia");
            foreach(string accion in new[]{"Registrar","Guardar","Desactivar"})
            {
                var metodo=typeof(ClientesController).GetMethod(accion);
                Comprobar(metodo.IsDefined(typeof(HttpPostAttribute),true) && metodo.IsDefined(typeof(ValidateAntiForgeryTokenAttribute),true),"POST con antifalsificación: "+accion);
            }
            foreach(string accion in new[]{"Editar","Guardar","Desactivar"})
            {
                var contexto=Contexto(accion,seguridad.ObtenerSesion(cajero,1));
                foreach(PermisoAttribute filtro in typeof(ClientesController).GetMethod(accion).GetCustomAttributes(typeof(PermisoAttribute),true))filtro.OnAuthorization(contexto);
                Comprobar(contexto.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)contexto.Result).StatusCode==403,"Filtro 403 al cajero en "+accion);
            }
            var registrar=Contexto("Registrar",seguridad.ObtenerSesion(cajero,1));
            foreach(PermisoAttribute filtro in typeof(ClientesController).GetMethod("Registrar").GetCustomAttributes(typeof(PermisoAttribute),true))filtro.OnAuthorization(registrar);
            Comprobar(registrar.Result==null,"Filtro admite registrar al cajero");
            var sinVentas=new UsuarioSesion();sinVentas.Permisos.Add("CLIENTES_VER");
            var controlador=new ClientesController();controlador.ControllerContext=new ControllerContext(new ContextoPrueba(sinVentas),new RouteData(),controlador);
            Comprobar(((HttpStatusCodeResult)controlador.Historial(id)).StatusCode==403,"Historial devuelve 403 sin permisos de ventas");
            var bind=(BindAttribute)typeof(ClientesController).GetMethod("Registrar").GetParameters()[0].GetCustomAttributes(typeof(BindAttribute),false).Single();
            Comprobar(!bind.Include.Contains("Id") && !bind.Include.Contains("EsGenerico") && typeof(SolicitudCliente).GetProperty("EsGenerico")==null,"Registro no admite sobreescritura de id ni conversión a genérico");
            Sql("UPDATE usuarios SET activo=false WHERE id=@id","id",cajero);Denegado(()=>clientes.Listar(cajero),"Permisos se comprueban de nuevo al desactivar usuario");
            Console.WriteLine("TOTAL: "+cantidad+" comprobaciones de clientes correctas.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
    }
    private static AuthorizationContext Contexto(string accion,UsuarioSesion usuario)
    {
        var descriptor=new ReflectedControllerDescriptor(typeof(ClientesController));
        return new AuthorizationContext(new ControllerContext(new ContextoPrueba(usuario),new RouteData(),new ClientesController()),new ReflectedActionDescriptor(typeof(ClientesController).GetMethod(accion),accion,descriptor));
    }
    private class ContextoPrueba:HttpContextBase
    {
        private readonly IDictionary items=new Hashtable();private readonly HttpRequestBase request=new SolicitudPrueba();private readonly HttpResponseBase response=new RespuestaPrueba();
        public ContextoPrueba(UsuarioSesion u){items["SistemaVentas.UsuarioActual"]=u;}
        public override IDictionary Items{get{return items;}}public override HttpRequestBase Request{get{return request;}}public override HttpResponseBase Response{get{return response;}}
    }
    private class SolicitudPrueba:HttpRequestBase
    {public override bool IsSecureConnection{get{return true;}}public override string RawUrl{get{return "/Clientes";}}}
    private class CachePrueba:HttpCachePolicyBase
    {public override void SetCacheability(HttpCacheability c){}public override void SetNoStore(){}}
    private class RespuestaPrueba:HttpResponseBase
    {private readonly HttpCachePolicyBase cache=new CachePrueba();public override HttpCachePolicyBase Cache{get{return cache;}}public override bool SuppressFormsAuthenticationRedirect{get;set;}}
}
