using System;
using System.Linq;
using System.Net.Mail;
using System.Text;
using SistemaVentas.Datos;
using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public sealed class ServicioProveedores
    {
        private readonly string cadena;
        public ServicioProveedores(string conexion){cadena=conexion;}
        private static void Exigir(ProveedoresRepositorio db,long actor,bool historial=false)
        {
            if(!db.Puede(actor,"PROVEEDORES_ADMINISTRAR") || (historial && !db.Puede(actor,"COMPRAS_ADMINISTRAR")))
                throw new OperacionNoPermitidaException("No tienes permiso para realizar esta operación de proveedores.");
        }
        public PaginaProveedores Listar(long actor,int pagina=1,string busqueda="",string estado="todos")
        {
            busqueda=(busqueda??"").Trim();if(busqueda.Length>150)busqueda=busqueda.Substring(0,150);
            if(estado!="activos" && estado!="inactivos")estado="todos";
            using(var db=new ProveedoresRepositorio(cadena,true)){Exigir(db,actor);return db.Listar(Pagina(pagina),busqueda,estado);}
        }
        public Proveedor Obtener(long actor,long id)
        {using(var db=new ProveedoresRepositorio(cadena,true)){Exigir(db,actor);return db.Obtener(id);}}
        public long Guardar(long actor,SolicitudProveedor solicitud)
        {
            var p=Validar(solicitud);
            using(var db=new ProveedoresRepositorio(cadena))
            {
                Exigir(db,actor);
                if(p.Id!=0)ValidarActual(db.Obtener(p.Id,true),p.Version);
                long id=db.Guardar(p);db.Auditar(actor,p.Id==0?"PROVEEDOR_CREADO":"PROVEEDOR_EDITADO",id);db.Confirmar();return id;
            }
        }
        public void Desactivar(long actor,long id,long version)
        {
            using(var db=new ProveedoresRepositorio(cadena))
            {
                Exigir(db,actor);var actual=db.Obtener(id,true);ValidarActual(actual,version);
                if(!actual.Activo)return;
                db.Desactivar(id,version);db.Auditar(actor,"PROVEEDOR_DESACTIVADO",id);db.Confirmar();
            }
        }
        public HistorialProveedor Historial(long actor,long id,int pagina=1)
        {
            using(var db=new ProveedoresRepositorio(cadena,true))
            {
                Exigir(db,actor,true);var proveedor=db.Obtener(id);
                return proveedor==null?null:db.Historial(proveedor,Pagina(pagina));
            }
        }
        private static int Pagina(int pagina){return Math.Max(1,Math.Min(1000000,pagina));}
        private static void ValidarActual(Proveedor actual,long version)
        {
            if(actual==null)throw new OperacionNoPermitidaException("No se encontró el proveedor.");
            if(version<=0 || version!=actual.Version)throw new OperacionNoPermitidaException("El proveedor cambió en otra ventana. Vuelve al listado y abre Editar nuevamente.");
        }
        private static string Texto(string texto,int maximo,string campo,bool obligatorio=false)
        {
            texto=(texto??"").Trim().Normalize(NormalizationForm.FormC);
            if(texto.Length>maximo || texto.Any(c=>char.IsControl(c) || c=='\u2028' || c=='\u2029') || (obligatorio && texto.Length==0))
                throw new OperacionNoPermitidaException(campo+" requiere "+(obligatorio?"de 1 a ":"hasta ")+maximo+" caracteres, sin saltos de línea ni caracteres de control.");
            return texto.Length==0?null:texto;
        }
        private static SolicitudProveedor Validar(SolicitudProveedor entrada)
        {
            if(entrada==null || entrada.Id<0)throw new OperacionNoPermitidaException("Revisa los datos del proveedor.");
            var p=new SolicitudProveedor{Id=entrada.Id,Version=entrada.Version,Nombre=Texto(entrada.Nombre,150,"El nombre",true),Ruc=Texto(entrada.Ruc,40,"El RUC"),
                Telefono=Texto(entrada.Telefono,40,"El teléfono"),Direccion=Texto(entrada.Direccion,250,"La dirección"),Correo=Texto(entrada.Correo,254,"El correo"),Activo=entrada.Activo};
            if(p.Correo!=null)
            {
                bool valido;
                try{var correo=new MailAddress(p.Correo);valido=correo.Address==p.Correo && !p.Correo.Any(char.IsWhiteSpace);}
                catch(FormatException){valido=false;}
                if(!valido)throw new OperacionNoPermitidaException("Escribe un correo electrónico válido, sin nombres adicionales.");
            }
            return p;
        }
    }
}
