using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web;
using System.Web.Routing;
using Npgsql;
using SistemaVentas.Entidades;
using SistemaVentas.Negocio;
using SistemaVentas.Web.Controllers;
using SistemaVentas.Web.Seguridad;

internal static class CatalogosPruebas
{
    private static int cantidad;
    private static string cadena;
    private static ServicioCategorias categorias;
    private static ServicioMarcas marcas;
    private static long admin;
    private static void Comprobar(bool valor, string descripcion)
    {
        if (!valor) throw new Exception("Falló: " + descripcion);
        cantidad++; Console.WriteLine("OK: " + descripcion);
    }
    private static void Denegado(Action accion, string descripcion)
    {
        bool fallo = false;
        try { accion(); } catch (OperacionNoPermitidaException) { fallo = true; }
        Comprobar(fallo, descripcion);
    }
    private static object Sql(string sql, params object[] pares)
    {
        using (var c = new NpgsqlConnection(cadena))
        {
            c.Open();
            using (var cmd = new NpgsqlCommand(sql,c))
            {
                cmd.CommandTimeout=15;
                for (int i=0;i<pares.Length;i+=2) cmd.Parameters.AddWithValue((string)pares[i],pares[i+1]??DBNull.Value);
                return cmd.ExecuteScalar();
            }
        }
    }
    private static void Integridad(Action accion, string descripcion)
    {
        bool fallo=false;
        try { accion(); } catch (PostgresException e) when(e.SqlState=="23503") { fallo=true; }
        Comprobar(fallo,descripcion);
    }
    private static long Producto(long categoria, object marca, bool activo=true)
    {
        return (long)Sql(@"INSERT INTO productos(codigo,descripcion,categoria_id,marca_id,unidad_medida_id,activo)
            VALUES (@codigo,'Producto de prueba',@categoria,@marca,(SELECT id FROM unidades_medida WHERE codigo='UN'),@activo) RETURNING id",
            "codigo",Guid.NewGuid().ToString("N"),"categoria",categoria,"marca",marca,"activo",activo);
    }
    private static int Main()
    {
        try
        {
            cadena=Environment.GetEnvironmentVariable("SV_TEST_CONNECTION");
            categorias=new ServicioCategorias(cadena); marcas=new ServicioMarcas(cadena);
            var seguridad=new ServicioSeguridad(cadena);
            admin=seguridad.CrearPrimerAdministrador("admin","Administrador de prueba",Guid.NewGuid().ToString("N")+"á!");
            var roles=seguridad.RolesParaUsuario(admin);
            long cajero=seguridad.GuardarUsuario(admin,new SolicitudUsuario { NombreUsuario="cajero",Nombre="Cajero de prueba",Password=Guid.NewGuid().ToString("N"),RolIds=new[]{roles.Single(r=>r.Codigo=="CAJERO").Id} });
            long supervisor=seguridad.GuardarUsuario(admin,new SolicitudUsuario { NombreUsuario="supervisor",Nombre="Supervisor de prueba",Password=Guid.NewGuid().ToString("N"),RolIds=new[]{roles.Single(r=>r.Codigo=="SUPERVISOR").Id} });
            Denegado(()=>categorias.Listar(cajero),"Cajero no lista categorías por llamada directa");
            Denegado(()=>marcas.Guardar(cajero,0,"Intrusión",true,0),"Cajero no crea marcas por llamada directa");
            Denegado(()=>categorias.Listar(0),"Actor inexistente rechazado");
            long c=categorias.Guardar(admin,0,"  Cafe\u0301  ",true,0);
            var dato=categorias.Obtener(admin,c);
            Comprobar(dato is Categoria && dato.Nombre=="Café" && dato.Activo && dato.Version>0,"Categoría real con recorte y normalización Unicode");
            long m=marcas.Guardar(supervisor,0,"Marca Ñandú",true,0);
            Comprobar(marcas.Obtener(admin,m) is Marca,"Supervisor administra marcas con su permiso");
            Denegado(()=>categorias.Guardar(admin,0," CAFÉ ",true,0),"Duplicados ignoran mayúsculas y espacios");
            Denegado(()=>categorias.Guardar(admin,0,"   ",true,0),"Nombre vacío rechazado");
            Denegado(()=>marcas.Guardar(admin,0,new string('a',101),true,0),"Nombre mayor a 100 rechazado");
            Denegado(()=>marcas.Guardar(admin,0,"Salto\nde línea",true,0),"Caracteres de control rechazados");
            long limite=marcas.Guardar(admin,0,new string('á',100),true,0);
            Comprobar(marcas.Obtener(admin,limite).Nombre.Length==100,"Nombre de 100 caracteres aceptado");
            long auditoria=(long)Sql("SELECT count(*) FROM auditoria WHERE entidad IN ('categorias','marcas')");
            Denegado(()=>categorias.Guardar(cajero,c,"Ataque",true,dato.Version),"Permiso comprobado también en edición");
            Denegado(()=>categorias.Desactivar(cajero,c,dato.Version),"Permiso comprobado en desactivación");
            Comprobar((long)Sql("SELECT count(*) FROM auditoria WHERE entidad IN ('categorias','marcas')")==auditoria,"Rechazos no registran éxitos ni cambios parciales");
            categorias.Guardar(admin,c,"Café molido",true,dato.Version);
            var editado=categorias.Obtener(admin,c);
            Denegado(()=>categorias.Guardar(admin,c,"Dato antiguo",true,dato.Version),"Edición obsoleta no sobrescribe cambios");
            Denegado(()=>categorias.Desactivar(admin,c,dato.Version),"Desactivación obsoleta rechazada");
            Comprobar(editado.Nombre=="Café molido" && editado.Version!=dato.Version,"Versión cambia con una actualización real");
            categorias.Guardar(admin,c,editado.Nombre,true,editado.Version);
            Comprobar(categorias.Obtener(admin,c).Version==editado.Version,"Guardar sin cambios no modifica la versión");
            categorias.Desactivar(admin,c,editado.Version);
            var inactivo=categorias.Obtener(admin,c);
            Comprobar(!inactivo.Activo,"Desactivación conserva el registro");
            Denegado(()=>categorias.Guardar(admin,0,"café molido",true,0),"Nombre inactivo sigue reservado");
            categorias.Guardar(admin,c,inactivo.Nombre,true,inactivo.Version);
            Comprobar(categorias.Obtener(admin,c).Activo,"Reactivación conserva el mismo identificador");
            long especial=categorias.Guardar(admin,0,"O'Hara <script>alert(1)</script>",false,0);
            Comprobar(categorias.Listar(admin,1,"O'Hara").Registros.Single().Id==especial,"Texto con apóstrofo se consulta con parámetros");
            Comprobar(categorias.Listar(admin,1,"' OR 1=1 --").Total==0,"Búsqueda no permite inyección SQL");
            for(int i=0;i<24;i++) categorias.Guardar(admin,0,"Página "+i.ToString("D2"),i%2==0,0);
            var pagina=categorias.Listar(admin,1,"Página"); var segunda=categorias.Listar(admin,2,"Página");
            Comprobar(pagina.Total==24 && pagina.Registros.Count==20 && segunda.Registros.Count==4,"Paginación SQL de 20 registros");
            Comprobar(!pagina.Registros.Select(x=>x.Id).Intersect(segunda.Registros.Select(x=>x.Id)).Any(),"Orden estable entre páginas");
            Comprobar(categorias.Listar(admin,999,"Página").Pagina==2 && categorias.Listar(admin,-4,"Página").Pagina==1,"Páginas fuera del rango se ajustan");
            Comprobar(categorias.Listar(admin,1,"Página","activos").Total==12 && categorias.Listar(admin,1,"Página","inactivos").Total==12,"Filtros de estado usan los datos reales");
            Comprobar(categorias.Listar(admin,1,"Página","estado inválido").Total==24,"Filtro desconocido usa Todos");
            long producto=Producto(c,m);
            Denegado(()=>categorias.Desactivar(admin,c,categorias.Obtener(admin,c).Version),"Producto activo impide desactivar categoría");
            Denegado(()=>marcas.Desactivar(admin,m,marcas.Obtener(admin,m).Version),"Producto activo impide desactivar marca");
            Integridad(()=>Sql("UPDATE categorias SET activo=false WHERE id=@id","id",c),"La FK protege también escrituras SQL directas");
            Comprobar(categorias.Obtener(admin,c).Activo && marcas.Obtener(admin,m).Activo,"Rechazo revierte ambas desactivaciones");
            Sql("UPDATE productos SET activo=false WHERE id=@id","id",producto);
            categorias.Desactivar(admin,c,categorias.Obtener(admin,c).Version);
            marcas.Desactivar(admin,m,marcas.Obtener(admin,m).Version);
            Comprobar((long)Sql("SELECT categoria_id FROM productos WHERE id=@id","id",producto)==c,"Producto inactivo conserva relaciones e historial");
            Integridad(()=>Sql("UPDATE productos SET activo=true WHERE id=@id","id",producto),"Producto no se reactiva con catálogos inactivos");
            Integridad(()=>Producto(c,null),"Producto activo exige categoría activa");
            var cat=categorias.Obtener(admin,c);categorias.Guardar(admin,c,cat.Nombre,true,cat.Version);
            Integridad(()=>Producto(c,m),"Producto activo exige marca activa cuando tiene marca");
            Comprobar(Producto(c,null)>0,"Marca opcional permite productos activos sin marca");
            Comprobar(Producto(c,m,false)>0,"Producto inactivo admite catálogos inactivos");
            Denegado(()=>marcas.Guardar(admin,m,new string('á',100),true,marcas.Obtener(admin,m).Version),"Duplicado en edición revierte la operación");
            Comprobar(!marcas.Obtener(admin,m).Activo,"Edición rechazada no reactiva parcialmente la marca");
            Denegado(()=>categorias.Guardar(admin,9999999,"No existe",true,1),"Editar un registro inexistente se rechaza");
            Comprobar(categorias.Obtener(admin,9999999)==null,"Consulta inexistente devuelve ausencia");
            // Dos ediciones con la misma versión: exactamente una puede confirmar.
            long simultaneo=marcas.Guardar(admin,0,"Simultáneo",true,0);
            long version=marcas.Obtener(admin,simultaneo).Version;
            int exitos=0,conflictos=0;
            Parallel.For(0,2,i=> {
                try { marcas.Guardar(admin,simultaneo,"Nombre concurrente "+i,true,version);Interlocked.Increment(ref exitos); }
                catch(OperacionNoPermitidaException) { Interlocked.Increment(ref conflictos); }
            });
            Comprobar(exitos==1 && conflictos==1,"Dos ediciones simultáneas no pierden cambios");
            // Inserción y desactivación simultáneas: nunca queda un producto activo con padre inactivo.
            for(int i=0;i<3;i++)
            {
                long carrera=categorias.Guardar(admin,0,"Carrera "+i,true,0);
                long v=categorias.Obtener(admin,carrera).Version;
                Task.WaitAll(Task.Run(()=> {
                    try { Producto(carrera,null); } catch(PostgresException e) when(e.SqlState=="23503") { }
                }),Task.Run(()=> {
                    try { categorias.Desactivar(admin,carrera,v); } catch(OperacionNoPermitidaException) { }
                }));
                Comprobar((long)Sql(@"SELECT count(*) FROM productos p JOIN categorias c ON c.id=p.categoria_id WHERE p.activo AND NOT c.activo")==0,"Carrera producto/desactivación mantiene integridad "+(i+1));
            }
            Sql("UPDATE roles SET activo=false WHERE codigo='SUPERVISOR'");
            Denegado(()=>marcas.Listar(supervisor),"Permiso revocado se comprueba en la siguiente operación");
            Comprobar((long)Sql("SELECT count(*) FROM auditoria WHERE entidad='categorias' AND entidad_id=@id AND accion='CATEGORIA_CREADA'","id",c)==1,"Creación auditada con actor y registro");
            Comprobar((long)Sql("SELECT count(*) FROM auditoria WHERE entidad='marcas' AND entidad_id=@id AND accion='MARCA_DESACTIVADA'","id",m)==1,"Desactivación auditada una sola vez");
            foreach(var tipo in new[]{typeof(CategoriasController),typeof(MarcasController)})
            {
                Comprobar(tipo.GetCustomAttributes(typeof(PermisoAttribute),true).Length==1,"Controlador exige permiso: "+tipo.Name);
                var filtro=(PermisoAttribute)tipo.GetCustomAttributes(typeof(PermisoAttribute),true).Single();
                var permitido=Contexto(tipo,seguridad.ObtenerSesion(admin,1));
                filtro.OnAuthorization(permitido);
                Comprobar(permitido.Result==null,"Filtro admite administrador: "+tipo.Name);
                var denegado=Contexto(tipo,seguridad.ObtenerSesion(cajero,1));
                filtro.OnAuthorization(denegado);
                Comprobar(denegado.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)denegado.Result).StatusCode==403,"Filtro responde 403 al cajero: "+tipo.Name);
                var anonimo=Contexto(tipo,null); filtro.OnAuthorization(anonimo);
                Comprobar(anonimo.Result is RedirectToRouteResult,"Filtro redirige al visitante sin sesión: "+tipo.Name);
                foreach(var accion in new[]{"Guardar","Desactivar"})
                {
                    var metodo=tipo.GetMethod(accion);
                    Comprobar(metodo.GetCustomAttributes(typeof(HttpPostAttribute),true).Length==1 && metodo.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute),true).Length==1,"POST y antifalsificación: "+tipo.Name+"."+accion);
                }
            }
            Console.WriteLine("TOTAL: "+cantidad+" comprobaciones de catálogos correctas."); return 0;
        }
        catch(Exception error) { Console.Error.WriteLine(error.GetType().Name+": "+error.Message); return 1; }
    }

    // Estos contextos comprueban el filtro MVC; no simulan una prueba HTTP completa.
    private static AuthorizationContext Contexto(Type tipo,UsuarioSesion usuario)
    {
        var controlador=(Controller)Activator.CreateInstance(tipo);
        var descriptor=new ReflectedControllerDescriptor(tipo);
        var accion=new ReflectedActionDescriptor(tipo.GetMethod("Index"),"Index",descriptor);
        return new AuthorizationContext(new ControllerContext(new ContextoPrueba(usuario),new RouteData(),controlador),accion);
    }
    private class ContextoPrueba:HttpContextBase
    {
        private readonly IDictionary items=new Hashtable();
        private readonly HttpRequestBase request=new SolicitudPrueba();
        private readonly HttpResponseBase response=new RespuestaPrueba();
        public ContextoPrueba(UsuarioSesion usuario){items["SistemaVentas.UsuarioActual"]=usuario;}
        public override IDictionary Items { get { return items; } }
        public override HttpRequestBase Request { get { return request; } }
        public override HttpResponseBase Response { get { return response; } }
    }
    private class SolicitudPrueba:HttpRequestBase
    {
        public override bool IsSecureConnection { get { return true; } }
        public override string RawUrl { get { return "/Categorias"; } }
    }
    private class CachePrueba:HttpCachePolicyBase
    { public override void SetCacheability(HttpCacheability valor){} public override void SetNoStore(){} }
    private class RespuestaPrueba:HttpResponseBase
    {
        private readonly HttpCachePolicyBase cache=new CachePrueba();
        public override HttpCachePolicyBase Cache { get { return cache; } }
        public override bool SuppressFormsAuthenticationRedirect { get;set; }
    }
}
