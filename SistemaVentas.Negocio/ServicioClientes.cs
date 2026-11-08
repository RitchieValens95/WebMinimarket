using System;
using System.Linq;
using System.Net.Mail;
using System.Text;
using SistemaVentas.Datos;
using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public sealed class ServicioClientes
    {
        private readonly string cadena;
        public ServicioClientes(string conexion){cadena=conexion;}
        private static void Exigir(ClientesRepositorio db,long actor,string permiso=null)
        {
            if(!db.Puede(actor,"CLIENTES_VER") || (permiso!=null && !db.Puede(actor,permiso)))
                throw new OperacionNoPermitidaException("No tienes permiso para realizar esta operación de clientes.");
        }
        public PaginaClientes Listar(long actor,int pagina=1,string busqueda="",string estado="todos")
        {
            busqueda=(busqueda??"").Trim();if(busqueda.Length>150) busqueda=busqueda.Substring(0,150);
            if(estado!="activos" && estado!="inactivos") estado="todos";
            using(var db=new ClientesRepositorio(cadena,true)){Exigir(db,actor);return db.Listar(Pagina(pagina),busqueda,estado);}
        }
        public Cliente Obtener(long actor,long id)
        { using(var db=new ClientesRepositorio(cadena,true)){Exigir(db,actor);return db.Obtener(id);} }
        public long Guardar(long actor,SolicitudCliente solicitud)
        {
            var p=Validar(solicitud);
            using(var db=new ClientesRepositorio(cadena))
            {
                Exigir(db,actor,p.Id==0?"CLIENTES_REGISTRAR":"CLIENTES_EDITAR");
                if(p.Id!=0) ValidarActual(db.Obtener(p.Id,true),p.Version);
                else if(!p.Activo && !db.Puede(actor,"CLIENTES_EDITAR"))
                    throw new OperacionNoPermitidaException("Para registrar un cliente inactivo necesitas permiso de edición. Regístralo como activo.");
                long id=db.Guardar(p);
                db.Auditar(actor,p.Id==0?"CLIENTE_CREADO":"CLIENTE_EDITADO",id);db.Confirmar();return id;
            }
        }
        public void Desactivar(long actor,long id,long version)
        {
            using(var db=new ClientesRepositorio(cadena))
            {
                Exigir(db,actor,"CLIENTES_EDITAR");var actual=db.Obtener(id,true);ValidarActual(actual,version);
                if(!actual.Activo) return;
                db.Desactivar(id,version);db.Auditar(actor,"CLIENTE_DESACTIVADO",id);db.Confirmar();
            }
        }
        public HistorialCliente Historial(long actor,long id,int pagina=1)
        {
            using(var db=new ClientesRepositorio(cadena,true))
            {
                Exigir(db,actor);bool todas=db.Puede(actor,"VENTAS_VER_TODAS");
                if(!todas && !db.Puede(actor,"VENTAS_VER_PROPIAS")) throw new OperacionNoPermitidaException("No tienes permiso para consultar ventas del cliente.");
                var cliente=db.Obtener(id);return cliente==null?null:db.Historial(cliente,actor,todas,Pagina(pagina));
            }
        }
        private static int Pagina(int pagina){return Math.Max(1,Math.Min(1000000,pagina));}
        private static void ValidarActual(Cliente actual,long version)
        {
            if(actual==null) throw new OperacionNoPermitidaException("No se encontró el cliente.");
            if(actual.EsGenerico) throw new OperacionNoPermitidaException("Consumidor final es el cliente genérico del sistema. Sus datos y su estado se conservan.");
            if(version<=0 || version!=actual.Version) throw new OperacionNoPermitidaException("El cliente cambió en otra ventana. Vuelve al listado y abre Editar nuevamente.");
        }
        private static string Texto(string texto,int maximo,string campo,bool obligatorio=false)
        {
            texto=(texto??"").Trim().Normalize(NormalizationForm.FormC);
            if(texto.Length>maximo || texto.Any(c=>char.IsControl(c) || c=='\u2028' || c=='\u2029') || (obligatorio && texto.Length==0))
                throw new OperacionNoPermitidaException(campo+" requiere "+(obligatorio?"de 1 a ":"hasta ")+maximo+" caracteres, sin saltos de línea ni caracteres de control.");
            return texto.Length==0?null:texto;
        }
        private static SolicitudCliente Validar(SolicitudCliente entrada)
        {
            if(entrada==null || entrada.Id<0) throw new OperacionNoPermitidaException("Revisa los datos del cliente.");
            var p=new SolicitudCliente{Id=entrada.Id,Version=entrada.Version,Nombre=Texto(entrada.Nombre,150,"El nombre",true),
                TipoDocumento=Texto(entrada.TipoDocumento,20,"El tipo de documento"),Documento=Texto(entrada.Documento,40,"El documento"),
                Telefono=Texto(entrada.Telefono,40,"El teléfono"),Direccion=Texto(entrada.Direccion,250,"La dirección"),Correo=Texto(entrada.Correo,254,"El correo"),Activo=entrada.Activo};
            if((p.TipoDocumento==null)!=(p.Documento==null)) throw new OperacionNoPermitidaException("Completa el tipo y el número de documento, o deja ambos vacíos.");
            if(p.TipoDocumento!=null)
            {
                p.TipoDocumento=p.TipoDocumento.ToUpperInvariant();
                if(!new[]{"CI","RUC","PASAPORTE","OTRO"}.Contains(p.TipoDocumento)) throw new OperacionNoPermitidaException("Selecciona un tipo de documento válido.");
            }
            if(p.Correo!=null)
            {
                bool valido;
                try{var correo=new MailAddress(p.Correo);valido=correo.Address==p.Correo && !p.Correo.Any(char.IsWhiteSpace);}
                catch(FormatException){valido=false;}
                if(!valido) throw new OperacionNoPermitidaException("Escribe un correo electrónico válido, sin nombres adicionales.");
            }
            return p;
        }
    }
}
