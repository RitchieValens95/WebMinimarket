using System;
using System.Data;
using Npgsql;
using SistemaVentas.Entidades;

namespace SistemaVentas.Datos
{
    public sealed class ProveedoresRepositorio : IDisposable
    {
        private readonly NpgsqlConnection conexion;
        private readonly NpgsqlTransaction transaccion;
        private const string Columnas="id,nombre,ruc,telefono,direccion,correo,activo,xmin::text::bigint";
        public ProveedoresRepositorio(string cadena,bool lectura=false)
        {
            conexion=ConexionPostgreSql.Abrir(cadena);
            try{transaccion=conexion.BeginTransaction(lectura?IsolationLevel.RepeatableRead:IsolationLevel.ReadCommitted);}
            catch{conexion.Dispose();throw;}
        }
        private NpgsqlCommand Comando(string sql,params object[] valores)
        {
            var cmd=new NpgsqlCommand(sql,conexion,transaccion){CommandTimeout=10};
            for(int i=0;i<valores.Length;i+=2)cmd.Parameters.AddWithValue((string)valores[i],valores[i+1]??DBNull.Value);
            return cmd;
        }
        public bool Puede(long actor,string permiso)
        {
            using(var cmd=Comando(@"SELECT EXISTS(SELECT 1 FROM public.usuarios u
                JOIN public.usuarios_roles ur ON ur.usuario_id=u.id JOIN public.roles r ON r.id=ur.rol_id
                JOIN public.roles_permisos rp ON rp.rol_id=r.id JOIN public.permisos p ON p.id=rp.permiso_id
                WHERE u.id=@actor AND u.activo AND r.activo AND p.codigo=@permiso)","actor",actor,"permiso",permiso))
                return (bool)cmd.ExecuteScalar();
        }
        private static string Texto(NpgsqlDataReader r,int columna){return r.IsDBNull(columna)?null:r.GetString(columna);}
        private static Proveedor Leer(NpgsqlDataReader r)
        {return new Proveedor{Id=r.GetInt64(0),Nombre=r.GetString(1),Ruc=Texto(r,2),Telefono=Texto(r,3),Direccion=Texto(r,4),Correo=Texto(r,5),Activo=r.GetBoolean(6),Version=r.GetInt64(7)};}
        public Proveedor Obtener(long id,bool bloquear=false)
        {
            using(var cmd=Comando("SELECT "+Columnas+" FROM public.proveedores WHERE id=@id"+(bloquear?" FOR UPDATE":""),"id",id))
            using(var r=cmd.ExecuteReader())return r.Read()?Leer(r):null;
        }
        public PaginaProveedores Listar(int pagina,string busqueda,string estado)
        {
            const string filtro=" WHERE (nombre ILIKE @q OR ruc ILIKE @q) AND (@estado='todos' OR activo=(@estado='activos'))";
            var resultado=new PaginaProveedores{Busqueda=busqueda,Estado=estado};
            using(var cmd=Comando("SELECT count(*) FROM public.proveedores"+filtro,"q","%"+busqueda+"%","estado",estado))resultado.Total=(long)cmd.ExecuteScalar();
            resultado.Pagina=Math.Min(pagina,(int)Math.Max(1,Math.Min(1000000,(resultado.Total+19)/20)));
            using(var cmd=Comando("SELECT "+Columnas+" FROM public.proveedores"+filtro+" ORDER BY lower(nombre),id LIMIT 20 OFFSET @offset",
                "q","%"+busqueda+"%","estado",estado,"offset",(resultado.Pagina-1)*20))
            using(var r=cmd.ExecuteReader())while(r.Read())resultado.Registros.Add(Leer(r));
            return resultado;
        }
        public long Guardar(SolicitudProveedor p)
        {
            string sql=p.Id==0
                ? @"INSERT INTO public.proveedores(nombre,ruc,telefono,direccion,correo,activo) VALUES (@nombre,@ruc,@telefono,@direccion,@correo,@activo) RETURNING id"
                : @"UPDATE public.proveedores SET nombre=@nombre,ruc=@ruc,telefono=@telefono,direccion=@direccion,correo=@correo,activo=@activo
                    WHERE id=@id AND xmin::text::bigint=@version RETURNING id";
            try
            {
                using(var cmd=Comando(sql,"nombre",p.Nombre,"ruc",p.Ruc,"telefono",p.Telefono,"direccion",p.Direccion,"correo",p.Correo,"activo",p.Activo,"id",p.Id,"version",p.Version))
                {
                    var id=cmd.ExecuteScalar();
                    if(id==null)throw new OperacionNoPermitidaException("El proveedor cambió en otra ventana. Vuelve al listado y abre Editar nuevamente.");
                    return (long)id;
                }
            }
            catch(PostgresException e)when(e.SqlState=="23505" && e.ConstraintName=="uq_proveedores_ruc")
            {throw new OperacionNoPermitidaException("Ya existe un proveedor con ese RUC, incluso si está inactivo. Revisa el registro existente.");}
        }
        public void Desactivar(long id,long version)
        {
            using(var cmd=Comando("UPDATE public.proveedores SET activo=false WHERE id=@id AND xmin::text::bigint=@version RETURNING id","id",id,"version",version))
                if(cmd.ExecuteScalar()==null)throw new OperacionNoPermitidaException("El proveedor cambió en otra ventana. Vuelve al listado y abre Editar nuevamente.");
        }
        public HistorialProveedor Historial(Proveedor proveedor,int pagina)
        {
            const string filtro=" WHERE c.proveedor_id=@id AND c.estado='CONFIRMADA'";
            var resultado=new HistorialProveedor{Proveedor=proveedor};
            using(var cmd=Comando("SELECT count(*),coalesce(sum(c.total),0) FROM public.compras c"+filtro,"id",proveedor.Id))
            using(var r=cmd.ExecuteReader()){r.Read();resultado.Total=r.GetInt64(0);resultado.ImporteTotal=r.GetDecimal(1);}
            resultado.Pagina=Math.Min(pagina,(int)Math.Max(1,Math.Min(1000000,(resultado.Total+19)/20)));
            using(var cmd=Comando(@"SELECT c.id,c.numero_documento,c.fecha,c.total,u.nombre FROM public.compras c JOIN public.usuarios u ON u.id=c.usuario_id"+filtro+
                " ORDER BY c.fecha DESC,c.id DESC LIMIT 20 OFFSET @offset","id",proveedor.Id,"offset",(resultado.Pagina-1)*20))
            using(var r=cmd.ExecuteReader())while(r.Read())resultado.Compras.Add(new CompraProveedor{Id=r.GetInt64(0),NumeroDocumento=Texto(r,1),Fecha=r.GetFieldValue<DateTimeOffset>(2),Total=r.GetDecimal(3),Usuario=r.GetString(4)});
            return resultado;
        }
        public void Auditar(long actor,string accion,long id)
        {
            using(var cmd=Comando("INSERT INTO public.auditoria(usuario_id,accion,entidad,entidad_id) VALUES (@actor,@accion,'proveedores',@id)","actor",actor,"accion",accion,"id",id))cmd.ExecuteNonQuery();
        }
        public void Confirmar(){transaccion.Commit();}
        public void Dispose(){transaccion.Dispose();conexion.Dispose();}
    }
}
