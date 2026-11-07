using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Npgsql;
using SistemaVentas.Entidades;

namespace SistemaVentas.Datos
{
    public sealed class ProductosRepositorio : IDisposable
    {
        private readonly NpgsqlConnection conexion;
        private readonly NpgsqlTransaction transaccion;
        private const string Columnas = @"p.id,p.codigo,p.descripcion,p.categoria_id,p.marca_id,p.unidad_medida_id,
            p.permite_decimales,p.precio_compra,p.precio_venta,p.stock_actual,p.stock_minimo,p.activo,
            p.xmin::text::bigint,c.nombre,coalesce(m.nombre,''),u.codigo";
        private const string Tablas = @" FROM public.productos p JOIN public.categorias c ON c.id=p.categoria_id
            LEFT JOIN public.marcas m ON m.id=p.marca_id JOIN public.unidades_medida u ON u.id=p.unidad_medida_id ";

        public ProductosRepositorio(string cadena, bool lectura = false)
        {
            conexion = ConexionPostgreSql.Abrir(cadena);
            try { transaccion = conexion.BeginTransaction(lectura ? IsolationLevel.RepeatableRead : IsolationLevel.ReadCommitted); }
            catch { conexion.Dispose(); throw; }
        }
        private NpgsqlCommand Comando(string sql, params object[] valores)
        {
            var cmd = new NpgsqlCommand(sql, conexion, transaccion) { CommandTimeout = 10 };
            for (int i = 0; i < valores.Length; i += 2)
                cmd.Parameters.AddWithValue((string)valores[i], valores[i + 1] ?? DBNull.Value);
            return cmd;
        }
        public bool Puede(long actor, string permiso)
        {
            using (var cmd = Comando(@"SELECT EXISTS(SELECT 1 FROM public.usuarios u
                JOIN public.usuarios_roles ur ON ur.usuario_id=u.id JOIN public.roles r ON r.id=ur.rol_id
                JOIN public.roles_permisos rp ON rp.rol_id=r.id JOIN public.permisos p ON p.id=rp.permiso_id
                WHERE u.id=@actor AND u.activo AND r.activo AND p.codigo=@permiso)", "actor", actor, "permiso", permiso))
                return (bool)cmd.ExecuteScalar();
        }
        public void PrepararEscritura()
        {
            using (var cmd = Comando("SELECT EXISTS(SELECT 1 FROM public.esquema_versiones WHERE version='006')"))
                if (!(bool)cmd.ExecuteScalar()) throw new OperacionNoPermitidaException("Completa la actualización de categorías y marcas antes de guardar productos.");
            // Serializa cambios de identificadores entre productos de la aplicación.
            using (var cmd = Comando("SELECT pg_advisory_xact_lock(783144982401)")) cmd.ExecuteNonQuery();
        }
        private static Producto Leer(NpgsqlDataReader r)
        {
            return new Producto {
                Id=r.GetInt64(0), Codigo=r.GetString(1), Descripcion=r.GetString(2), CategoriaId=r.GetInt64(3),
                MarcaId=r.IsDBNull(4)?(long?)null:r.GetInt64(4), UnidadMedidaId=r.GetInt64(5), PermiteDecimales=r.GetBoolean(6),
                PrecioCompra=r.GetDecimal(7), PrecioVenta=r.GetDecimal(8), StockActual=r.GetDecimal(9), StockMinimo=r.GetDecimal(10),
                Activo=r.GetBoolean(11), Version=r.GetInt64(12), Categoria=r.GetString(13), Marca=r.GetString(14), UnidadMedida=r.GetString(15)
            };
        }
        public Producto Obtener(long id, bool bloquear = false)
        {
            Producto producto;
            using (var cmd = Comando("SELECT " + Columnas + Tablas + " WHERE p.id=@id" + (bloquear ? " FOR UPDATE OF p" : ""), "id", id))
            using (var r = cmd.ExecuteReader()) producto = r.Read() ? Leer(r) : null;
            if (producto != null)
                using (var cmd = Comando("SELECT codigo FROM public.productos_codigos_barras WHERE producto_id=@id ORDER BY codigo", "id", id))
                using (var r = cmd.ExecuteReader()) while (r.Read()) producto.CodigosBarras.Add(r.GetString(0));
            return producto;
        }
        public PaginaProductos Listar(FiltroProductos f)
        {
            const string filtro = @" WHERE (p.descripcion ILIKE @q OR lower(btrim(p.codigo))=lower(@texto)
                OR EXISTS(SELECT 1 FROM public.productos_codigos_barras b WHERE b.producto_id=p.id AND b.codigo=@texto))
                AND (@estado='todos' OR p.activo=(@estado='activos'))
                AND (@categoria=0 OR p.categoria_id=@categoria) AND (@marca=0 OR p.marca_id=@marca)
                AND (@stock='todos' OR (@stock='bajo' AND p.stock_actual<=p.stock_minimo) OR (@stock='agotado' AND p.stock_actual=0)) ";
            var parametros = new object[] { "q", "%"+f.Busqueda+"%", "texto", f.Busqueda, "estado", f.Estado,
                "categoria", f.CategoriaId??0, "marca", f.MarcaId??0, "stock", f.Stock };
            var resultado = new PaginaProductos { Filtro=f };
            using (var cmd = Comando("SELECT count(*)"+Tablas+filtro, parametros)) resultado.Total=(long)cmd.ExecuteScalar();
            f.Pagina=Math.Min(f.Pagina,(int)Math.Max(1,Math.Min(1000000,(resultado.Total+19)/20)));
            using (var cmd = Comando("SELECT "+Columnas+Tablas+filtro+@" ORDER BY
                CASE WHEN lower(btrim(p.codigo))=lower(@texto) OR EXISTS(SELECT 1 FROM public.productos_codigos_barras b WHERE b.producto_id=p.id AND b.codigo=@texto) THEN 0 ELSE 1 END,
                lower(p.descripcion),p.id LIMIT 20 OFFSET @offset", parametros.Concat(new object[]{"offset",(f.Pagina-1)*20}).ToArray()))
            using (var r = cmd.ExecuteReader()) while (r.Read()) resultado.Registros.Add(Leer(r));
            return resultado;
        }
        public OpcionesProducto Opciones(long? categoria, long? marca, bool incluirInactivos = false)
        {
            var opciones = new OpcionesProducto();
            using (var cmd = Comando(@"SELECT id,nombre || CASE WHEN activo THEN '' ELSE ' (inactiva)' END FROM public.categorias
                WHERE activo OR @todos OR id=@id ORDER BY lower(nombre),id", "id", categoria??0, "todos", incluirInactivos))
            using (var r = cmd.ExecuteReader()) while(r.Read()) opciones.Categorias.Add(new OpcionProducto{Id=r.GetInt64(0),Nombre=r.GetString(1)});
            using (var cmd = Comando(@"SELECT id,nombre || CASE WHEN activo THEN '' ELSE ' (inactiva)' END FROM public.marcas
                WHERE activo OR @todos OR id=@id ORDER BY lower(nombre),id", "id", marca??0, "todos", incluirInactivos))
            using (var r = cmd.ExecuteReader()) while(r.Read()) opciones.Marcas.Add(new OpcionProducto{Id=r.GetInt64(0),Nombre=r.GetString(1)});
            using (var cmd = Comando("SELECT id,codigo || ' - ' || nombre FROM public.unidades_medida ORDER BY codigo"))
            using (var r = cmd.ExecuteReader()) while(r.Read()) opciones.Unidades.Add(new OpcionProducto{Id=r.GetInt64(0),Nombre=r.GetString(1)});
            return opciones;
        }
        public bool TieneHistorial(long id)
        {
            using (var cmd = Comando(@"SELECT EXISTS(SELECT 1 FROM public.movimientos_stock WHERE producto_id=@id)
                OR EXISTS(SELECT 1 FROM public.ventas_detalles WHERE producto_id=@id)
                OR EXISTS(SELECT 1 FROM public.compras_detalles WHERE producto_id=@id)", "id", id)) return (bool)cmd.ExecuteScalar();
        }
        public void ValidarReferencias(SolicitudProducto p)
        {
            // Mantener el orden categoría → marca → unidad → producto evita invertir
            // los bloqueos que usa la desactivación de los catálogos.
            using (var cmd = Comando("SELECT id FROM public.categorias WHERE id=@id AND (activo OR NOT @activo) FOR KEY SHARE", "id",p.CategoriaId,"activo",p.Activo))
                if(cmd.ExecuteScalar()==null) throw new OperacionNoPermitidaException("Selecciona una categoría válida y activa para un producto activo.");
            if(p.MarcaId.HasValue)
                using (var cmd = Comando("SELECT id FROM public.marcas WHERE id=@id AND (activo OR NOT @activo) FOR KEY SHARE", "id",p.MarcaId.Value,"activo",p.Activo))
                    if(cmd.ExecuteScalar()==null) throw new OperacionNoPermitidaException("Selecciona una marca válida y activa para un producto activo, o elige Sin marca.");
            using (var cmd = Comando("SELECT id FROM public.unidades_medida WHERE id=@id FOR KEY SHARE", "id",p.UnidadMedidaId))
                if(cmd.ExecuteScalar()==null) throw new OperacionNoPermitidaException("Selecciona una unidad de medida válida.");
        }
        public void ValidarIdentificadores(SolicitudProducto p)
        {
            // Los índices únicos siguen siendo la última protección para códigos y barras.
            using (var cmd = Comando(@"SELECT EXISTS(SELECT 1 FROM public.productos WHERE id<>@id AND
                    (lower(btrim(codigo))=lower(@codigo) OR lower(btrim(codigo)) IN (SELECT lower(x) FROM unnest(@barras) x)))
                OR EXISTS(SELECT 1 FROM public.productos_codigos_barras WHERE producto_id<>@id AND
                    (lower(codigo)=lower(@codigo) OR codigo=ANY(@exactas)))",
                "id",p.Id,"codigo",p.Codigo,"barras",p.CodigosBarras.ToArray(),"exactas",p.CodigosBarras.ToArray()))
                if ((bool)cmd.ExecuteScalar()) throw new OperacionNoPermitidaException("El código o uno de los códigos de barras ya identifica otro producto, incluso si está inactivo.");
        }
        public long Guardar(SolicitudProducto p)
        {
            try { return GuardarInterno(p); }
            catch(PostgresException e) when(e.SqlState=="23505")
            { throw new OperacionNoPermitidaException("El código o un código de barras ya existe. Revisa también los productos inactivos."); }
            catch(PostgresException e) when(e.SqlState=="23503")
            { throw new OperacionNoPermitidaException("La categoría, marca o unidad seleccionada cambió. Recarga el formulario y revisa que esté disponible."); }
        }
        private long GuardarInterno(SolicitudProducto p)
        {
            string sql = p.Id==0
                ? @"INSERT INTO public.productos(codigo,descripcion,categoria_id,marca_id,unidad_medida_id,permite_decimales,precio_compra,precio_venta,stock_minimo,activo)
                    VALUES (@codigo,@descripcion,@categoria,@marca,@unidad,@decimales,@compra,@venta,@minimo,@activo) RETURNING id"
                : @"UPDATE public.productos SET codigo=@codigo,descripcion=@descripcion,categoria_id=@categoria,marca_id=@marca,unidad_medida_id=@unidad,
                    permite_decimales=@decimales,precio_compra=@compra,precio_venta=@venta,stock_minimo=@minimo,activo=@activo
                    WHERE id=@id AND xmin::text::bigint=@version RETURNING id";
            long id;
            using (var cmd = Comando(sql, "codigo",p.Codigo,"descripcion",p.Descripcion,"categoria",p.CategoriaId,"marca",p.MarcaId,
                "unidad",p.UnidadMedidaId,"decimales",p.PermiteDecimales,"compra",p.PrecioCompra,"venta",p.PrecioVenta,"minimo",p.StockMinimo,
                "activo",p.Activo,"id",p.Id,"version",p.Version))
            {
                object resultado=cmd.ExecuteScalar();
                if (resultado==null) throw new OperacionNoPermitidaException("El producto cambió en otra ventana. Vuelve al listado y abre Editar nuevamente.");
                id=(long)resultado;
            }
            // Solo se sincroniza el conjunto cambiado; los códigos conservados mantienen su id.
            using (var cmd = Comando("DELETE FROM public.productos_codigos_barras WHERE producto_id=@id AND NOT (codigo=ANY(@barras))", "id",id,"barras",p.CodigosBarras.ToArray())) cmd.ExecuteNonQuery();
            foreach (string barra in p.CodigosBarras)
                using (var cmd = Comando(@"INSERT INTO public.productos_codigos_barras(producto_id,codigo)
                    SELECT @id,@codigo WHERE NOT EXISTS(SELECT 1 FROM public.productos_codigos_barras WHERE producto_id=@id AND codigo=@codigo)","id",id,"codigo",barra)) cmd.ExecuteNonQuery();
            return id;
        }
        public void Desactivar(long id, long version)
        {
            using (var cmd = Comando("UPDATE public.productos SET activo=false WHERE id=@id AND xmin::text::bigint=@version RETURNING id", "id",id,"version",version))
                if (cmd.ExecuteScalar()==null) throw new OperacionNoPermitidaException("El producto cambió en otra ventana. Vuelve al listado y abre Editar nuevamente.");
        }
        public HistorialProducto Historial(Producto producto, int pagina)
        {
            var resultado=new HistorialProducto{Producto=producto};
            using (var cmd=Comando("SELECT count(*) FROM public.movimientos_stock WHERE producto_id=@id","id",producto.Id)) resultado.Total=(long)cmd.ExecuteScalar();
            resultado.Pagina=Math.Min(pagina,(int)Math.Max(1,Math.Min(1000000,(resultado.Total+19)/20)));
            using (var cmd=Comando(@"SELECT m.id,m.fecha,m.tipo,m.cantidad,m.saldo_anterior,m.saldo_posterior,m.costo_unitario,u.nombre,coalesce(m.observacion,'')
                FROM public.movimientos_stock m JOIN public.usuarios u ON u.id=m.usuario_id WHERE m.producto_id=@id
                ORDER BY m.fecha DESC,m.id DESC LIMIT 20 OFFSET @offset","id",producto.Id,"offset",(resultado.Pagina-1)*20))
            using (var r=cmd.ExecuteReader()) while(r.Read()) resultado.Movimientos.Add(new MovimientoProducto{Id=r.GetInt64(0),Fecha=r.GetFieldValue<DateTimeOffset>(1),
                Tipo=r.GetString(2),Cantidad=r.GetDecimal(3),SaldoAnterior=r.GetDecimal(4),SaldoPosterior=r.GetDecimal(5),CostoUnitario=r.GetDecimal(6),Usuario=r.GetString(7),Observacion=r.GetString(8)});
            return resultado;
        }
        public void Auditar(long actor, string accion, long id)
        {
            using (var cmd=Comando("INSERT INTO public.auditoria(usuario_id,accion,entidad,entidad_id) VALUES (@actor,@accion,'productos',@id)", "actor",actor,"accion",accion,"id",id)) cmd.ExecuteNonQuery();
        }
        public void Confirmar() { transaccion.Commit(); }
        public void Dispose() { transaccion.Dispose(); conexion.Dispose(); }
    }
}
