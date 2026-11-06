using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Xml;
using Npgsql;
using SistemaVentas.Entidades;
using SistemaVentas.Negocio;
using SistemaVentas.Web.Controllers;
using SistemaVentas.Web.Seguridad;

internal static class SeguridadPruebas
{
    private static int cantidad;
    private static string cadena;
    private static ServicioSeguridad servicio;
    private static string Password() { return Guid.NewGuid().ToString("N") + "-áÑ!"; }
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
            using (var cmd = new NpgsqlCommand(sql, c))
            {
                for (int i = 0; i < pares.Length; i += 2) cmd.Parameters.AddWithValue((string)pares[i], pares[i + 1]);
                return cmd.ExecuteScalar();
            }
        }
    }
    private static SolicitudUsuario Solicitud(UsuarioAdministrable u)
    {
        return new SolicitudUsuario { Id=u.Id, NombreUsuario=u.NombreUsuario, Nombre=u.Nombre, Correo=u.Correo, Activo=u.Activo, RolIds=u.RolIds.ToArray() };
    }

    private static int Main(string[] args)
    {
        try
        {
            cadena = Environment.GetEnvironmentVariable("SV_TEST_CONNECTION");
            servicio = new ServicioSeguridad(cadena);
            Comprobar((long)Sql("SELECT count(*) FROM usuarios") == 0, "Base temporal sin cuentas predeterminadas");
            var pw = Password();
            var hash1 = Contrasenas.Crear(pw);
            var hash2 = Contrasenas.Crear(pw);
            Comprobar(hash1 != hash2, "Sal aleatoria distinta para la misma contraseña");
            Comprobar(hash1.StartsWith("v1$pbkdf2-sha256$600000$") && !hash1.Contains(pw), "Formato versionado sin texto plano");
            Comprobar(Contrasenas.Verificar(pw,hash1), "Hash acepta la contraseña Unicode correcta");
            Comprobar(!Contrasenas.Verificar("incorrecta",hash1), "Hash rechaza contraseña incorrecta");
            Comprobar(!Contrasenas.Verificar(pw,"v1$pbkdf2-sha256$999999999$a$b"), "Hash corrupto no exige trabajo ilimitado");
            Denegado(() => servicio.CrearPrimerAdministrador("admin","Administrador","corta"), "Bootstrap exige contraseña larga");
            var admin = servicio.CrearPrimerAdministrador("admin", "Administrador de pruebas", pw);
            Denegado(() => servicio.CrearPrimerAdministrador("segundo","Otro administrador",pw), "Bootstrap no se repite en una instalación con cuentas");
            var sesion = servicio.Ingresar(" ADMIN ",pw);
            Comprobar(sesion != null && sesion.Id == admin && sesion.Puede("ROLES_ADMINISTRAR"), "Inicio de sesión y permisos desde PostgreSQL");
            Comprobar(servicio.ObtenerUsuario(admin,admin).UltimoAcceso.HasValue, "Último acceso registrado");
            Comprobar((long)Sql("SELECT count(*) FROM auditoria WHERE accion='LOGIN_CORRECTO'")>0, "Acceso correcto auditado");
            Comprobar(servicio.Ingresar("' OR 1=1--",pw)==null, "Entrada SQL no autentica ni altera consultas");
            Denegado(() => servicio.ListarUsuarios(999999,1,""), "Servicio rechaza actor inexistente");
            var roles = servicio.RolesParaUsuario(admin);
            long cajero = roles.Single(r=>r.Codigo=="CAJERO").Id;
            long adminRol = roles.Single(r=>r.Codigo=="ADMINISTRADOR").Id;
            Denegado(() => servicio.GuardarUsuario(admin,new SolicitudUsuario { NombreUsuario="sinrol",Nombre="Sin rol",Password=pw }), "Usuario activo requiere un rol");
            var pwCajero = Password();
            long cajeroId = servicio.GuardarUsuario(admin,new SolicitudUsuario { NombreUsuario="cajero01",Nombre="Cajero de prueba",Password=pwCajero,RolIds=new[]{cajero} });
            var sesionCajero = servicio.Ingresar("cajero01",pwCajero);
            Comprobar(sesionCajero.Puede("VENTAS_CONFIRMAR") && !sesionCajero.Puede("USUARIOS_ADMINISTRAR"), "Cajero obtiene solo sus permisos");
            Denegado(() => servicio.GuardarUsuario(cajeroId,new SolicitudUsuario { NombreUsuario="intruso",Nombre="Intruso",Password=pw,RolIds=new[]{adminRol} }), "Cajero no crea administradores por llamada directa");
            Denegado(() => servicio.ListarRoles(cajeroId), "Cajero no consulta administración de roles");
            Denegado(() => servicio.GuardarUsuario(admin,new SolicitudUsuario { NombreUsuario="CAJERO01",Nombre="Duplicado",Password=pw,RolIds=new[]{cajero} }), "Usuario duplicado se rechaza sin distinguir mayúsculas");
            for (int i=0;i<5;i++) Comprobar(servicio.Ingresar("cajero01","equivocada")==null,"Intento fallido " +(i+1));
            Comprobar(servicio.Ingresar("cajero01",pwCajero)==null,"Cuenta bloqueada rechaza incluso contraseña correcta");
            Comprobar((int)Sql("SELECT intentos_fallidos FROM usuarios WHERE id=@id","id",cajeroId)==5,"Contador de fallos limitado a cinco");
            Sql("UPDATE usuarios SET bloqueado_hasta=CURRENT_TIMESTAMP-interval '1 second' WHERE id=@id","id",cajeroId);
            Comprobar(servicio.Ingresar("cajero01",pwCajero)!=null,"Acceso vuelve a funcionar después del bloqueo");
            Comprobar((int)Sql("SELECT intentos_fallidos FROM usuarios WHERE id=@id","id",cajeroId)==0,"Acceso correcto reinicia el contador");
            var edit = Solicitud(servicio.ObtenerUsuario(admin,cajeroId));edit.Nombre="Cajero editado";
            servicio.GuardarUsuario(admin,edit);
            Comprobar(servicio.ObtenerSesion(cajeroId,sesionCajero.VersionSesion)==null,"Editar invalida cookies anteriores");
            sesionCajero=servicio.Ingresar("cajero01",pwCajero);
            Comprobar(sesionCajero.Nombre=="Cajero editado","Editar sin contraseña conserva el hash anterior");
            servicio.DesactivarUsuario(admin,cajeroId);
            Comprobar(servicio.ObtenerSesion(cajeroId,sesionCajero.VersionSesion)==null && servicio.Ingresar("cajero01",pwCajero)==null,"Desactivar revoca sesión y acceso");
            edit=Solicitud(servicio.ObtenerUsuario(admin,cajeroId));edit.Activo=true;edit.Password=Password();pwCajero=edit.Password;
            servicio.GuardarUsuario(admin,edit);
            Comprobar(servicio.Ingresar("cajero01",pwCajero)!=null,"Administrador reactiva y cambia la contraseña");
            var permisos=servicio.ListarPermisos(admin);
            long verUsuarios=permisos.Single(p=>p.Codigo=="USUARIOS_VER").Id;
            long verStock=permisos.Single(p=>p.Codigo=="STOCK_VER").Id;
            long rolNuevo=servicio.GuardarRol(admin,new SolicitudRol { Codigo="CONSULTA",Nombre="Consulta temporal",PermisoIds=new[]{verUsuarios} });
            edit=Solicitud(servicio.ObtenerUsuario(admin,cajeroId));edit.RolIds=new[]{cajero,rolNuevo};servicio.GuardarUsuario(admin,edit);
            sesionCajero=servicio.Ingresar("cajero01",pwCajero);
            Comprobar(sesionCajero.Puede("USUARIOS_VER"),"Asignación de varios roles combina permisos");
            servicio.GuardarRol(admin,new SolicitudRol { Id=rolNuevo,Codigo="CONSULTA",Nombre="Consulta actualizada",PermisoIds=new[]{verStock} });
            var refrescada=servicio.ObtenerSesion(cajeroId,sesionCajero.VersionSesion);
            Comprobar(!refrescada.Puede("USUARIOS_VER") && refrescada.Puede("STOCK_VER"),"Cambiar permisos tiene efecto sin volver a entrar");
            servicio.GuardarRol(admin,new SolicitudRol { Id=rolNuevo,Codigo="CONSULTA",Nombre="Consulta actualizada",Activo=false,PermisoIds=new[]{verStock} });
            Comprobar(!servicio.ObtenerSesion(cajeroId,sesionCajero.VersionSesion).Puede("STOCK_VER"),"Rol inactivo deja de conceder permisos");
            Denegado(() => servicio.DesactivarUsuario(admin,admin),"No se desactiva al último administrador");
            Comprobar(servicio.ObtenerUsuario(admin,admin).Activo,"La operación rechazada revierte sus cambios");
            Denegado(() => servicio.GuardarRol(admin,new SolicitudRol { Id=adminRol,Codigo="ADMINISTRADOR",Nombre="Administrador",PermisoIds=new long[0] }),"No se quitan los permisos de recuperación al último administrador");
            Comprobar(servicio.ListarRoles(admin).Single(r=>r.Id==adminRol).PermisoIds.Count==26,"Permisos conservados tras reversión");
            Denegado(() => servicio.GuardarRol(admin,new SolicitudRol { Id=adminRol,Codigo="CAMBIADO",Nombre="Cambio",PermisoIds=permisos.Select(p=>p.Id).ToArray() }),"Código de rol existente es inmutable");
            var pwOtro=Password();
            long otro=servicio.GuardarUsuario(admin,new SolicitudUsuario { NombreUsuario="admin02",Nombre="Segundo administrador",Password=pwOtro,RolIds=new[]{adminRol} });
            int exitos=0;
            Parallel.Invoke(()=>{try{servicio.DesactivarUsuario(admin,admin);System.Threading.Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){}},
                ()=>{try{servicio.DesactivarUsuario(otro,otro);System.Threading.Interlocked.Increment(ref exitos);}catch(OperacionNoPermitidaException){}});
            Comprobar(exitos==1,"Dos desactivaciones simultáneas conservan un administrador");
            long superviviente=(long)Sql("SELECT id FROM usuarios WHERE activo AND nombre_usuario IN ('admin','admin02')");
            var actual=servicio.Ingresar(superviviente==admin?"admin":"admin02",superviviente==admin?pw:pwOtro);
            Comprobar(actual!=null,"Administrador superviviente mantiene acceso");
            servicio.RegistrarSalida(actual.Id,actual.VersionSesion);
            Comprobar(servicio.ObtenerSesion(actual.Id,actual.VersionSesion)==null,"Cerrar sesión invalida la versión en el servidor");
            actual=servicio.Ingresar(superviviente==admin?"admin":"admin02",superviviente==admin?pw:pwOtro);
            servicio.RegistrarSalida(actual.Id,actual.VersionSesion-1);
            Comprobar(servicio.ObtenerSesion(actual.Id,actual.VersionSesion)!=null,"Una cookie obsoleta no revoca una sesión nueva");
            Comprobar(servicio.ListarUsuarios(actual.Id,1,"';DROP TABLE usuarios;--").Total==0,"Búsqueda parametrizada no ejecuta SQL recibido");
            Sql("INSERT INTO usuarios(nombre_usuario,nombre,password_hash,activo) SELECT '__pagina_'||n,'Paginación '||n,(SELECT password_hash FROM usuarios LIMIT 1),false FROM generate_series(1,25)n");
            var primera=servicio.ListarUsuarios(actual.Id,1,"__pagina_");var segunda=servicio.ListarUsuarios(actual.Id,2,"__pagina_");
            Comprobar(primera.Total==25 && primera.Usuarios.Count==20 && segunda.Usuarios.Count==5,"Paginación y total reales desde PostgreSQL");
            ProbarWeb(actual,servicio.ObtenerSesion(cajeroId,sesionCajero.VersionSesion));
            Console.WriteLine("TOTAL: "+cantidad+" comprobaciones correctas.");return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1; }
    }

    private static void ProbarWeb(UsuarioSesion admin, UsuarioSesion cajero)
    {
        foreach(var tipo in new[]{typeof(AccesoController),typeof(UsuariosController),typeof(RolesController)})
            foreach(var metodo in tipo.GetMethods().Where(m=>m.IsDefined(typeof(HttpPostAttribute),true)))
                Comprobar(metodo.IsDefined(typeof(ValidateAntiForgeryTokenAttribute),true),"CSRF en "+tipo.Name+"."+metodo.Name);
        var filtro=new PermisoAttribute("ROLES_ADMINISTRAR");
        var conPermiso=Contexto(admin,true);filtro.OnAuthorization(conPermiso);
        Comprobar(conPermiso.Result==null,"Filtro permite al administrador autorizado");
        var sinPermiso=Contexto(cajero,true);filtro.OnAuthorization(sinPermiso);
        Comprobar(sinPermiso.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)sinPermiso.Result).StatusCode==403,"URL de administración denegada al cajero");
        var anonimo=Contexto(null,true);filtro.OnAuthorization(anonimo);
        Comprobar(anonimo.Result is RedirectToRouteResult,"Acceso sin sesión redirige al ingreso");
        var http=Contexto(admin,false);filtro.OnAuthorization(http);
        Comprobar(http.Result is HttpStatusCodeResult && ((HttpStatusCodeResult)http.Result).StatusCode==400,"No se admite sesión sobre HTTP sin TLS");
        var helper=new UrlHelper(new RequestContext(new ContextoFalso(null,true),new RouteData()));
        Comprobar(helper.IsLocalUrl("/Inicio") && !helper.IsLocalUrl("https://externo.example/") && !helper.IsLocalUrl("//externo.example/"),"Retorno de acceso restringido a URL local");
        var controlador=new InicioController();var cc=new ControllerContext(new ContextoFalso(admin,true),new RouteData(),controlador);
        var error=new ExceptionContext(cc,new Exception("password=NO_MOSTRAR"));new ErroresSegurosAttribute().OnException(error);
        Comprobar(error.ExceptionHandled && !((ContentResult)error.Result).Content.Contains("NO_MOSTRAR"),"Errores no exponen detalles internos");
        var csrf=new ExceptionContext(cc,new HttpAntiForgeryException());new ErroresSegurosAttribute().OnException(csrf);
        Comprobar(csrf.HttpContext.Response.StatusCode==400,"Token CSRF inválido produce 400 controlado");
        var doc=new XmlDocument();doc.Load("/app/Web.config");
        Comprobar(doc.SelectSingleNode("/configuration/system.web/httpCookies").Attributes["httpOnlyCookies"].Value=="true" &&
            doc.SelectSingleNode("/configuration/system.web/authentication/forms").Attributes["requireSSL"].Value=="true","Cookies HttpOnly y autenticación HTTPS configuradas");
    }

    private static AuthorizationContext Contexto(UsuarioSesion u,bool https)
    {
        var controlador=new InicioController();
        var descriptor=new ReflectedControllerDescriptor(typeof(InicioController));
        var accion=new ReflectedActionDescriptor(typeof(InicioController).GetMethod("Index"),"Index",descriptor);
        return new AuthorizationContext(new ControllerContext(new ContextoFalso(u,https),new RouteData(),controlador),accion);
    }
    private class SolicitudFalsa:HttpRequestBase
    {
        private readonly bool https;public SolicitudFalsa(bool valor){https=valor;}
        public override bool IsSecureConnection{get{return https;}}
        public override string RawUrl{get{return "/Usuarios";}}
        public override string ApplicationPath{get{return "/";}}
    }
    private class CacheFalso:HttpCachePolicyBase
    {public override void SetCacheability(HttpCacheability c){}public override void SetNoStore(){} }
    private class RespuestaFalsa:HttpResponseBase
    {
        private readonly HttpCachePolicyBase cache=new CacheFalso();
        public override HttpCachePolicyBase Cache{get{return cache;}}
        public override bool SuppressFormsAuthenticationRedirect{get;set;}
        public override bool TrySkipIisCustomErrors{get;set;}
        public override int StatusCode{get;set;}
    }
    private class ContextoFalso:HttpContextBase
    {
        private readonly IDictionary items=new Hashtable();private readonly HttpRequestBase request;private readonly HttpResponseBase response=new RespuestaFalsa();
        public ContextoFalso(UsuarioSesion u,bool https){items["SistemaVentas.UsuarioActual"]=u;request=new SolicitudFalsa(https);}
        public override IDictionary Items{get{return items;}}
        public override HttpRequestBase Request{get{return request;}}
        public override HttpResponseBase Response{get{return response;}}
        public override IPrincipal User{get;set;}
    }
}
