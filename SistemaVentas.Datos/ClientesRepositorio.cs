using System;
using System.Data;
using Npgsql;
using SistemaVentas.Entidades;

namespace SistemaVentas.Datos
{
    public sealed class ClientesRepositorio : IDisposable
    {
        private readonly NpgsqlConnection conexion;
        private readonly NpgsqlTransaction transaccion;
        private const string Columnas = "id,nombre,tipo_documento,documento,telefono,direccion,correo,activo,es_generico,xmin::text::bigint";
        public ClientesRepositorio(string cadena,bool lectura=false)
        {
            conexion=ConexionPostgreSql.Abrir(cadena);
            try { transaccion=conexion.BeginTransaction(lectura?IsolationLevel.RepeatableRead:IsolationLevel.ReadCommitted); }
            catch { conexion.Dispose();throw; }
        }
        private NpgsqlCommand Comando(string sql,params object[] valores)
        {
            var cmd=new NpgsqlCommand(sql,conexion,transaccion){CommandTimeout=10};
            for(int i=0;i<valores.Length;i+=2) cmd.Parameters.AddWithValue((string)valores[i],valores[i+1]??DBNull.Value);
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
        private static string Texto(NpgsqlDataReader r,int columna) { return r.IsDBNull(columna)?null:r.GetString(columna); }
        private static Cliente Leer(NpgsqlDataReader r)
        {
            return new Cliente{Id=r.GetInt64(0),Nombre=r.GetString(1),TipoDocumento=Texto(r,2),Documento=Texto(r,3),Telefono=Texto(r,4),
                Direccion=Texto(r,5),Correo=Texto(r,6),Activo=r.GetBoolean(7),EsGenerico=r.GetBoolean(8),Version=r.GetInt64(9)};
        }
        public Cliente Obtener(long id,bool bloquear=false)
        {
            using(var cmd=Comando("SELECT "+Columnas+" FROM public.clientes WHERE id=@id"+(bloquear?" FOR UPDATE":""),"id",id))
            using(var r=cmd.ExecuteReader()) return r.Read()?Leer(r):null;
        }
        public PaginaClientes Listar(int pagina,string busqueda,string estado)
        {
            const string filtro=" WHERE (nombre ILIKE @q OR documento ILIKE @q) AND (@estado='todos' OR activo=(@estado='activos'))";
            var resultado=new PaginaClientes{Busqueda=busqueda,Estado=estado};
            using(var cmd=Comando("SELECT count(*) FROM public.clientes"+filtro,"q","%"+busqueda+"%","estado",estado)) resultado.Total=(long)cmd.ExecuteScalar();
            resultado.Pagina=Math.Min(pagina,(int)Math.Max(1,Math.Min(1000000,(resultado.Total+19)/20)));
            using(var cmd=Comando("SELECT "+Columnas+" FROM public.clientes"+filtro+" ORDER BY es_generico DESC,lower(nombre),id LIMIT 20 OFFSET @offset",
                "q","%"+busqueda+"%","estado",estado,"offset",(resultado.Pagina-1)*20))
            using(var r=cmd.ExecuteReader()) while(r.Read()) resultado.Registros.Add(Leer(r));
            return resultado;
        }
        public long Guardar(SolicitudCliente p)
        {
            string sql=p.Id==0
                ? @"INSERT INTO public.clientes(nombre,tipo_documento,documento,telefono,direccion,correo,activo)
                    VALUES (@nombre,@tipo,@documento,@telefono,@direccion,@correo,@activo) RETURNING id"
                : @"UPDATE public.clientes SET nombre=@nombre,tipo_documento=@tipo,documento=@documento,telefono=@telefono,direccion=@direccion,correo=@correo,activo=@activo
                    WHERE id=@id AND NOT es_generico AND xmin::text::bigint=@version RETURNING id";
            try
            {
                using(var cmd=Comando(sql,"nombre",p.Nombre,"tipo",p.TipoDocumento,"documento",p.Documento,"telefono",p.Telefono,
                    "direccion",p.Direccion,"correo",p.Correo,"activo",p.Activo,"id",p.Id,"version",p.Version))
                {
                    var id=cmd.ExecuteScalar();
                    if(id==null) throw new OperacionNoPermitidaException("El cliente cambió en otra ventana. Vuelve al listado y abre Editar nuevamente.");
                    return (long)id;
                }
            }
            catch(PostgresException e)when(e.SqlState=="23505" && e.ConstraintName=="uq_clientes_documento")
            { throw new OperacionNoPermitidaException("Ya existe un cliente con ese tipo y número de documento, incluso si está inactivo. Revisa el registro existente."); }
        }
        public void Desactivar(long id,long version)
        {
            using(var cmd=Comando("UPDATE public.clientes SET activo=false WHERE id=@id AND NOT es_generico AND xmin::text::bigint=@version RETURNING id","id",id,"version",version))
                if(cmd.ExecuteScalar()==null) throw new OperacionNoPermitidaException("El cliente cambió en otra ventana. Vuelve al listado y abre Editar nuevamente.");
        }
        public HistorialCliente Historial(Cliente cliente,long actor,bool todas,int pagina)
        {
            const string filtro=" WHERE v.cliente_id=@cliente AND v.estado='CONFIRMADA' AND (@todas OR v.usuario_id=@actor)";
            var resultado=new HistorialCliente{Cliente=cliente,SoloPropias=!todas};
            using(var cmd=Comando("SELECT count(*),coalesce(sum(v.total),0) FROM public.ventas v"+filtro,"cliente",cliente.Id,"actor",actor,"todas",todas))
            using(var r=cmd.ExecuteReader()) { r.Read();resultado.Total=r.GetInt64(0);resultado.ImporteTotal=r.GetDecimal(1); }
            resultado.Pagina=Math.Min(pagina,(int)Math.Max(1,Math.Min(1000000,(resultado.Total+19)/20)));
            using(var cmd=Comando(@"SELECT v.numero,v.fecha,v.total,u.nombre FROM public.ventas v JOIN public.usuarios u ON u.id=v.usuario_id"+filtro+
                " ORDER BY v.fecha DESC,v.id DESC LIMIT 20 OFFSET @offset","cliente",cliente.Id,"actor",actor,"todas",todas,"offset",(resultado.Pagina-1)*20))
            using(var r=cmd.ExecuteReader()) while(r.Read()) resultado.Compras.Add(new CompraCliente{Numero=r.GetInt64(0),Fecha=r.GetFieldValue<DateTimeOffset>(1),Total=r.GetDecimal(2),Vendedor=r.GetString(3)});
            return resultado;
        }
        public void Auditar(long actor,string accion,long id)
        {
            using(var cmd=Comando("INSERT INTO public.auditoria(usuario_id,accion,entidad,entidad_id) VALUES (@actor,@accion,'clientes',@id)","actor",actor,"accion",accion,"id",id)) cmd.ExecuteNonQuery();
        }
        public void Confirmar(){transaccion.Commit();}
        public void Dispose(){transaccion.Dispose();conexion.Dispose();}
    }
}
