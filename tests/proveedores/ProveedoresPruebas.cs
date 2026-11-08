using System;
using System.Collections;
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

internal static class ProveedoresPruebas
{
    private static int cantidad;
    private static string cadena;
    private static long producto;
    private static ServicioProveedores proveedores;
    private static void Comprobar(bool valor,string descripcion)
    {if(!valor)throw new Exception("Falló: "+descripcion);cantidad++;Console.WriteLine("OK: "+descripcion);}
    private static void Denegado(Action accion,string descripcion)
    {bool rechazo=false;try{accion();}catch(OperacionNoPermitidaException){rechazo=true;}Comprobar(rechazo,descripcion);}
    private static object Ejecutar(NpgsqlConnection c,NpgsqlTransaction t,string sql,params object[] valores)
    {using(var cmd=new NpgsqlCommand(sql,c,t)){for(int i=0;i<valores.Length;i+=2)cmd.Parameters.AddWithValue((string)valores[i],valores[i+1]??DBNull.Value);return cmd.ExecuteScalar();}}
    private static object Sql(string sql,params object[] valores)
    {using(var c=new NpgsqlConnection(cadena)){c.Open();return Ejecutar(c,null,sql,valores);}}
    private static SolicitudProveedor Nuevo(string nombre="Proveedor de prueba")
    {return new SolicitudProveedor{Nombre=nombre,Ruc=Guid.NewGuid().ToString("N"),Telefono="+595 981 123456",Direccion="Dirección de prueba",Correo="proveedor@example.com"};}
    // Compra completa exclusivamente en la base temporal. No implementa el módulo de compras.
    private static long Compra(long actor,long proveedor,bool confirmar=true,bool sinDocumento=false)
    {
        using(var c=new NpgsqlConnection(cadena))
        {
            c.Open();using(var t=c.BeginTransaction())
            {
                long compra=(long)Ejecutar(c,t,@"INSERT INTO compras(proveedor_id,usuario_id,numero_documento,clave_idempotencia,solicitud_hash,total)
                    VALUES (@proveedor,@actor,@documento,@clave,repeat('b',64),10) RETURNING id","proveedor",proveedor,"actor",actor,
                    "documento",sinDocumento?null:"DOC-"+Guid.NewGuid().ToString("N"),"clave",Guid.NewGuid());
                if(confirmar)
                {
                    long detalle=(long)Ejecutar(c,t,"INSERT INTO compras_detalles(compra_id,producto_id,cantidad,costo_unitario,subtotal) VALUES (@compra,@producto,1,10,10) RETURNING id","compra",compra,"producto",producto);
                    Ejecutar(c,t,"UPDATE productos SET stock_actual=stock_actual+1 WHERE id=@id","id",producto);
                    Ejecutar(c,t,@"INSERT INTO movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,compra_detalle_id)
                        SELECT id,@actor,'ENTRADA_COMPRA',1,stock_actual-1,stock_actual,10,@detalle FROM productos WHERE id=@id","actor",actor,"detalle",detalle,"id",producto);
                    Ejecutar(c,t,"INSERT INTO auditoria(usuario_id,accion,entidad,entidad_id) VALUES (@actor,'COMPRA_CONFIRMADA','compras',@compra)","actor",actor,"compra",compra);
                    Ejecutar(c,t,"UPDATE compras SET estado='CONFIRMADA' WHERE id=@id","id",compra);
                }
                t.Commit();return compra;
            }
        }
    }
    private static int Main()
    {
        try
        {
            Console.OutputEncoding=System.Text.Encoding.UTF8;
            cadena=Environment.GetEnvironmentVariable("SV_TEST_CONNECTION");proveedores=new ServicioProveedores(cadena);
            var seguridad=new ServicioSeguridad(cadena);
            long admin=seguridad.CrearPrimerAdministrador("admin","Administrador",Guid.NewGuid().ToString("N"));
            var roles=seguridad.RolesParaUsuario(admin);
            long supervisor=seguridad.GuardarUsuario(admin,new SolicitudUsuario{NombreUsuario="supervisor",Nombre="Supervisor de prueba",Password=Guid.NewGuid().ToString("N"),RolIds=new[]{roles.Single(r=>r.Codigo=="SUPERVISOR").Id}});
            long cajero=seguridad.GuardarUsuario(admin,new SolicitudUsuario{NombreUsuario="cajero",Nombre="Cajero de prueba",Password=Guid.NewGuid().ToString("N"),RolIds=new[]{roles.Single(r=>r.Codigo=="CAJERO").Id}});
            var sesionCajero=seguridad.ObtenerSesion(cajero,1);
            Comprobar(proveedores.Listar(admin).Total==0,"Listado inicial vacío sin proveedores ficticios");
            Denegado(()=>proveedores.Listar(cajero),"Cajero no consulta proveedores por llamada directa");
            Denegado(()=>proveedores.Guardar(cajero,Nuevo()),"Cajero no registra proveedores");
            Denegado(()=>proveedores.Listar(0),"Actor inexistente rechazado");
            var alta=Nuevo("  Cafe\u0301 del Sur  ");alta.Ruc="  RUC-ABC-001 ";
            long id=proveedores.Guardar(supervisor,alta);var p=proveedores.Obtener(admin,id);
            Comprobar(p.Nombre=="Café del Sur" && p.Ruc=="RUC-ABC-001" && p.Activo && p.Version>0,"Supervisor registra y normaliza proveedor");
            Comprobar(p.Telefono==alta.Telefono && p.Direccion==alta.Direccion && p.Correo==alta.Correo,"Contactos reales guardados en PostgreSQL");
            var repetido=Nuevo();repetido.Ruc=" ruc-abc-001 ";Denegado(()=>proveedores.Guardar(admin,repetido),"RUC duplicado ignora mayúsculas y espacios");
            var ceros=Nuevo("Con ceros");ceros.Ruc="0000123-4";long cerosId=proveedores.Guardar(admin,ceros);
            Comprobar(proveedores.Obtener(admin,cerosId).Ruc=="0000123-4","RUC conserva ceros y guiones");
            var opcional=Nuevo("Sin RUC");opcional.Ruc=" ";opcional.Telefono=null;opcional.Direccion=" ";opcional.Correo=null;
            long opcionalId=proveedores.Guardar(admin,opcional);var sinDatos=proveedores.Obtener(admin,opcionalId);
            Comprobar(sinDatos.Ruc==null && sinDatos.Telefono==null && sinDatos.Direccion==null && sinDatos.Correo==null,"Opcionales vacíos se guardan como NULL");
            opcional.Nombre="Otro sin RUC";Comprobar(proveedores.Guardar(admin,opcional)!=opcionalId,"Varios proveedores sin RUC no generan duplicados ficticios");
            var invalido=Nuevo(" ");Denegado(()=>proveedores.Guardar(admin,invalido),"Nombre vacío rechazado");
            invalido=Nuevo(new string('a',151));Denegado(()=>proveedores.Guardar(admin,invalido),"Nombre mayor a 150 rechazado");
            invalido=Nuevo("Nombre\ncon control");Denegado(()=>proveedores.Guardar(admin,invalido),"Nombre con caracteres de control rechazado");
            invalido=Nuevo();invalido.Ruc=new string('1',41);Denegado(()=>proveedores.Guardar(admin,invalido),"RUC mayor a 40 rechazado");
            invalido=Nuevo();invalido.Telefono=new string('1',41);Denegado(()=>proveedores.Guardar(admin,invalido),"Teléfono mayor a 40 rechazado");
            invalido=Nuevo();invalido.Direccion=new string('a',251);Denegado(()=>proveedores.Guardar(admin,invalido),"Dirección mayor a 250 rechazada");
            invalido=Nuevo();invalido.Correo="incorrecto";Denegado(()=>proveedores.Guardar(admin,invalido),"Correo mal formado rechazado");
            invalido=Nuevo();invalido.Correo="Nombre <proveedor@example.com>";Denegado(()=>proveedores.Guardar(admin,invalido),"Correo con nombre adicional rechazado");
            var anterior=proveedores.Obtener(admin,id);p=proveedores.Obtener(admin,id);p.Nombre="Proveedor editado";p.Telefono="0981 000001";proveedores.Guardar(admin,p);
            var editado=proveedores.Obtener(admin,id);Comprobar(editado.Nombre=="Proveedor editado" && editado.Telefono=="0981 000001" && editado.Version!=anterior.Version,"Edición cambia datos y versión");
            long auditoria=(long)Sql("SELECT count(*) FROM auditoria WHERE entidad='proveedores'");
            Denegado(()=>proveedores.Guardar(admin,anterior),"Formulario antiguo no sobrescribe cambios");
            Denegado(()=>proveedores.Desactivar(admin,id,anterior.Version),"Desactivación obsoleta rechazada");
            Denegado(()=>proveedores.Desactivar(cajero,id,editado.Version),"Cajero no desactiva proveedores");
            Comprobar((long)Sql("SELECT count(*) FROM auditoria WHERE entidad='proveedores'")==auditoria,"Rechazos no auditan éxitos parciales");
            proveedores.Desactivar(admin,id,editado.Version);p=proveedores.Obtener(admin,id);
            Comprobar(!p.Activo && p.Ruc=="RUC-ABC-001","Desactivación conserva ID y RUC");
            Denegado(()=>proveedores.Guardar(admin,repetido),"RUC de proveedor inactivo sigue reservado");
            p.Activo=true;proveedores.Guardar(admin,p);Comprobar(proveedores.Obtener(admin,id).Activo,"Reactivación conserva registro");
            var otro=Nuevo("Proveedor inactivo");otro.Activo=false;long otroId=proveedores.Guardar(admin,otro);
            Comprobar(proveedores.Listar(admin,1,"","inactivos").Registros.Single().Id==otroId,"Filtro de proveedores inactivos");
            Comprobar(proveedores.Listar(admin,1,"editado").Registros.Single().Id==id && proveedores.Listar(admin,1,"abc-001").Registros.Single().Id==id,"Búsqueda por nombre y RUC");
            Comprobar(proveedores.Listar(admin,1,"' OR 1=1 --").Total==0,"Búsqueda parametrizada no permite inyección");
            for(int i=0;i<23;i++)proveedores.Guardar(admin,Nuevo("Página "+i.ToString("D2")));
            var primera=proveedores.Listar(admin,1,"Página");var segunda=proveedores.Listar(admin,2,"Página");
            Comprobar(primera.Total==23 && primera.Registros.Count==20 && segunda.Registros.Count==3,"Paginación SQL de 20 proveedores");
            Comprobar(!primera.Registros.Select(x=>x.Id).Intersect(segunda.Registros.Select(x=>x.Id)).Any(),"Páginas con orden estable sin repetición");
            Comprobar(proveedores.Listar(admin,999,"Página").Pagina==2 && proveedores.Listar(admin,-1,"Página").Pagina==1,"Páginas fuera de rango se ajustan");
            int exitos=0,conflictos=0;var a=proveedores.Obtener(admin,id);var b=proveedores.Obtener(admin,id);a.Nombre="Ventana A";b.Nombre="Ventana B";
            Parallel.Invoke(()=>{try{proveedores.Guardar(admin,a);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}},
                ()=>{try{proveedores.Guardar(admin,b);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}});
            Comprobar(exitos==1 && conflictos==1,"Ediciones simultáneas no pierden cambios");
            exitos=0;conflictos=0;
            Parallel.For(0,2,i=>{var n=Nuevo("Duplicado concurrente");n.Ruc="CONCURRENTE";try{proveedores.Guardar(admin,n);Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){Interlocked.Increment(ref conflictos);}});
            Comprobar(exitos==1 && conflictos==1 && (long)Sql("SELECT count(*) FROM proveedores WHERE ruc='CONCURRENTE'")==1,"Unicidad concurrente confirma un solo proveedor");
            Comprobar(proveedores.Historial(admin,id).Total==0 && proveedores.Historial(supervisor,id).ImporteTotal==0,"Historial vacío sin compras simuladas en la aplicación");
            producto=(long)Sql(@"INSERT INTO productos(codigo,descripcion,categoria_id,unidad_medida_id)
                SELECT 'TEST-PROVEEDOR','Producto temporal',c.id,u.id FROM categorias c CROSS JOIN unidades_medida u WHERE c.nombre='General' AND u.codigo='UN' RETURNING id");
            for(int i=0;i<25;i++)Compra(admin,id);
            Compra(admin,id,false);Compra(supervisor,otroId);Compra(supervisor,id,true,true);
            var historial=proveedores.Historial(admin,id);var pagina2=proveedores.Historial(admin,id,2);
            Comprobar(historial.Total==26 && historial.ImporteTotal==260m && historial.Compras.Count==20,"Historial usa compras confirmadas reales, sin borradores");
            Comprobar(pagina2.Compras.Count==6 && proveedores.Historial(admin,id,999).Pagina==2,"Historial con paginación de 20 compras");
            Comprobar(!historial.Compras.Select(x=>x.Id).Intersect(pagina2.Compras.Select(x=>x.Id)).Any(),"Historial sin compras repetidas entre páginas");
            Comprobar(historial.Compras.Any(x=>x.NumeroDocumento==null),"Compra confirmada sin número documental se consulta sin inventarlo");
            Comprobar(proveedores.Historial(supervisor,id).Total==26 && proveedores.Historial(admin,otroId).Total==1,"Historial respeta proveedor y permiso global de compras");
            long comprasAntes=(long)Sql("SELECT count(*) FROM compras WHERE proveedor_id=@id","id",id);decimal stockAntes=(decimal)Sql("SELECT stock_actual FROM productos WHERE id=@id","id",producto);
            p=proveedores.Obtener(admin,id);p.Direccion="Dirección actualizada";proveedores.Guardar(admin,p);p=proveedores.Obtener(admin,id);proveedores.Desactivar(admin,id,p.Version);
            Comprobar(proveedores.Historial(admin,id).Total==26 && (long)Sql("SELECT count(*) FROM compras WHERE proveedor_id=@id","id",id)==comprasAntes && (decimal)Sql("SELECT stock_actual FROM productos WHERE id=@id","id",producto)==stockAntes,"Editar o desactivar conserva documentos e inventario");
            long soloProveedores=seguridad.GuardarRol(admin,new SolicitudRol{Codigo="SOLO_PROVEEDORES",Nombre="Administrar proveedores",PermisoIds=new[]{seguridad.ListarPermisos(admin).Single(x=>x.Codigo=="PROVEEDORES_ADMINISTRAR").Id}});
            var u=seguridad.ObtenerUsuario(admin,cajero);seguridad.GuardarUsuario(admin,new SolicitudUsuario{Id=cajero,NombreUsuario=u.NombreUsuario,Nombre=u.Nombre,Activo=true,RolIds=new[]{soloProveedores}});
            Comprobar(proveedores.Obtener(cajero,id)!=null,"Rol con permiso de proveedores consulta datos de contacto");
            Denegado(()=>proveedores.Historial(cajero,id),"Administrar proveedores no revela compras sin permiso adicional");
            Comprobar(proveedores.Obtener(admin,99999999)==null && proveedores.Historial(admin,99999999)==null,"Proveedor inexistente devuelve ausencia");
            Denegado(()=>proveedores.Guardar(admin,new SolicitudProveedor{Id=99999999,Version=1,Nombre="Inexistente"}),"Edición inexistente rechazada");
            Comprobar((long)Sql("SELECT count(*) FROM auditoria WHERE entidad='proveedores' AND entidad_id=@id AND accion='PROVEEDOR_CREADO'","id",id)==1,"Creación auditada con actor e identificador");
            foreach(string accion in new[]{"Guardar","Desactivar"})
            {
                var metodo=typeof(ProveedoresController).GetMethod(accion);
                Comprobar(metodo.IsDefined(typeof(HttpPostAttribute),true) && metodo.IsDefined(typeof(ValidateAntiForgeryTokenAttribute),true),"POST con antifalsificación: "+accion);
            }
            var filtro=(PermisoAttribute)typeof(ProveedoresController).GetCustomAttributes(typeof(PermisoAttribute),true).Single();
            foreach(string accion in new[]{"Index","Crear","Editar","Guardar","Desactivar","Historial"})
            {
                var contexto=Contexto(accion,sesionCajero);filtro.OnAuthorization(contexto);
                Comprobar(contexto.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)contexto.Result).StatusCode==403,"Filtro 403 al cajero sin permiso: "+accion);
            }
            var solo=new UsuarioSesion();solo.Permisos.Add("PROVEEDORES_ADMINISTRAR");var compras=Contexto("Historial",solo);
            foreach(PermisoAttribute f in typeof(ProveedoresController).GetMethod("Historial").GetCustomAttributes(typeof(PermisoAttribute),true))f.OnAuthorization(compras);
            Comprobar(compras.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)compras.Result).StatusCode==403,"Historial requiere permiso de compras también en MVC");
            Sql("UPDATE usuarios SET activo=false WHERE id=@id","id",cajero);Denegado(()=>proveedores.Listar(cajero),"Acceso revocado se comprueba en la siguiente operación");
            Console.WriteLine("TOTAL: "+cantidad+" comprobaciones de proveedores correctas.");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
    }
    private static AuthorizationContext Contexto(string accion,UsuarioSesion usuario)
    {
        var descriptor=new ReflectedControllerDescriptor(typeof(ProveedoresController));
        return new AuthorizationContext(new ControllerContext(new ContextoPrueba(usuario),new RouteData(),new ProveedoresController()),new ReflectedActionDescriptor(typeof(ProveedoresController).GetMethod(accion),accion,descriptor));
    }
    private class ContextoPrueba:HttpContextBase
    {
        private readonly IDictionary items=new Hashtable();private readonly HttpRequestBase request=new SolicitudPrueba();private readonly HttpResponseBase response=new RespuestaPrueba();
        public ContextoPrueba(UsuarioSesion u){items["SistemaVentas.UsuarioActual"]=u;}
        public override IDictionary Items{get{return items;}}public override HttpRequestBase Request{get{return request;}}public override HttpResponseBase Response{get{return response;}}
    }
    private class SolicitudPrueba:HttpRequestBase
    {public override bool IsSecureConnection{get{return true;}}public override string RawUrl{get{return "/Proveedores";}}}
    private class CachePrueba:HttpCachePolicyBase
    {public override void SetCacheability(HttpCacheability c){}public override void SetNoStore(){}}
    private class RespuestaPrueba:HttpResponseBase
    {private readonly HttpCachePolicyBase cache=new CachePrueba();public override HttpCachePolicyBase Cache{get{return cache;}}public override bool SuppressFormsAuthenticationRedirect{get;set;}}
}
