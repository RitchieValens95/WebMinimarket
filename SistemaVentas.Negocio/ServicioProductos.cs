using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SistemaVentas.Datos;
using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public sealed class ServicioProductos
    {
        private readonly string cadena;
        public ServicioProductos(string conexion) { cadena=conexion; }
        private static void Exigir(ProductosRepositorio db, long actor, bool editar=false, bool historial=false)
        {
            if (!db.Puede(actor,"PRODUCTOS_VER") || (editar && !db.Puede(actor,"PRODUCTOS_EDITAR")) || (historial && !db.Puede(actor,"STOCK_VER")))
                throw new OperacionNoPermitidaException("No tienes permiso para realizar esta operación de productos.");
        }
        public PaginaProductos Listar(long actor, FiltroProductos filtro=null)
        {
            filtro=filtro??new FiltroProductos();
            // Copiar los filtros evita cambiar el objeto del llamador.
            var f=new FiltroProductos { Pagina=Pagina(filtro.Pagina),Busqueda=(filtro.Busqueda??"").Trim(),Estado=filtro.Estado,
                CategoriaId=filtro.CategoriaId,MarcaId=filtro.MarcaId,Stock=filtro.Stock };
            if(f.Busqueda.Length>200) f.Busqueda=f.Busqueda.Substring(0,200);
            if(f.Estado!="activos" && f.Estado!="inactivos") f.Estado="todos";
            if(f.Stock!="bajo" && f.Stock!="agotado") f.Stock="todos";
            if(f.CategoriaId<=0) f.CategoriaId=null;
            if(f.MarcaId<=0) f.MarcaId=null;
            using(var db=new ProductosRepositorio(cadena,true)) { Exigir(db,actor);return db.Listar(f,CodigoBalanza.Leer(f.Busqueda)); }
        }
        public Producto Obtener(long actor,long id)
        {
            using(var db=new ProductosRepositorio(cadena,true)) { Exigir(db,actor);return db.Obtener(id); }
        }
        public OpcionesProducto Opciones(long actor,long? categoria=null,long? marca=null,bool paraFormulario=false)
        {
            using(var db=new ProductosRepositorio(cadena,true))
            { Exigir(db,actor,paraFormulario);return db.Opciones(categoria,marca,!paraFormulario); }
        }
        public HistorialProducto Historial(long actor,long id,int pagina=1)
        {
            using(var db=new ProductosRepositorio(cadena,true))
            {
                Exigir(db,actor,historial:true);
                var producto=db.Obtener(id);
                return producto==null?null:db.Historial(producto,Pagina(pagina));
            }
        }
        public long ReservarNuevo(long actor)
        {
            using(var db=new ProductosRepositorio(cadena))
            {
                Exigir(db,actor,editar:true);db.PrepararEscritura();
                long id=db.ReservarId();
                Ean13.Generar(id); // Verificar capacidad antes de mostrar el formulario.
                db.Confirmar();return id;
            }
        }
        public long Guardar(long actor,SolicitudProducto solicitud)
        {
            var p=Validar(solicitud);
            using(var db=new ProductosRepositorio(cadena))
            {
                Exigir(db,actor,editar:true);db.PrepararEscritura();
                db.ValidarReferencias(p);
                if(p.Id!=0)
                {
                    var actual=db.Obtener(p.Id,true);ValidarVersion(actual,p.Version);
                    if((p.UnidadMedidaId!=actual.UnidadMedidaId || p.PermiteDecimales!=actual.PermiteDecimales) &&
                        (actual.StockActual!=0 || db.TieneHistorial(p.Id)))
                        throw new OperacionNoPermitidaException("Este producto ya tiene existencias o historial. Conserva su unidad de medida y su configuración de cantidades decimales.");
                }
                if(p.Id==0)
                {
                    if(p.IdNuevo==0) p.IdNuevo=db.ReservarId();
                    if(db.Obtener(p.IdNuevo)!=null) throw new OperacionNoPermitidaException("Este producto ya fue guardado. Vuelve al listado antes de crear otro.");
                }
                long idCodigo=p.Id==0?p.IdNuevo:p.Id;
                if(p.BalanzaPesable || p.BalanzaUnitario) p.CodigoBarra=CodigoBalanza.Prefijo(idCodigo,p.BalanzaUnitario);
                else if(p.GenerarCodigoBarras) p.CodigoBarra=Ean13.Generar(idCodigo);
                db.ValidarIdentificadores(p);
                long id=db.Guardar(p);
                db.Auditar(actor,p.Id==0?"PRODUCTO_CREADO":"PRODUCTO_EDITADO",id);
                db.Confirmar();return id;
            }
        }
        public void Desactivar(long actor,long id,long version)
        {
            using(var db=new ProductosRepositorio(cadena))
            {
                Exigir(db,actor,editar:true);db.PrepararEscritura();
                var actual=db.Obtener(id,true);ValidarVersion(actual,version);
                if(!actual.Activo) return;
                db.Desactivar(id,version);db.Auditar(actor,"PRODUCTO_DESACTIVADO",id);db.Confirmar();
            }
        }
        private static int Pagina(int pagina) { return Math.Max(1,Math.Min(1000000,pagina)); }
        private static void ValidarVersion(Producto actual,long version)
        {
            if(actual==null) throw new OperacionNoPermitidaException("No se encontró el producto.");
            if(version<=0 || version!=actual.Version) throw new OperacionNoPermitidaException("El producto cambió en otra ventana. Vuelve al listado y abre Editar nuevamente.");
        }
        private static string Texto(string valor,int maximo,string campo)
        {
            valor=(valor??"").Trim().Normalize(NormalizationForm.FormC);
            if(valor.Length==0 || valor.Length>maximo || valor.Any(char.IsControl))
                throw new OperacionNoPermitidaException(campo+" requiere de 1 a "+maximo+" caracteres, sin saltos de línea ni caracteres de control.");
            return valor;
        }
        private static void Numero(decimal valor,decimal maximo,int escala,string campo)
        {
            if(valor<0 || valor>maximo || decimal.Round(valor,escala)!=valor)
                throw new OperacionNoPermitidaException(campo+" debe ser positivo o cero, con hasta "+escala+" decimales y dentro del rango admitido.");
        }
        private static SolicitudProducto Validar(SolicitudProducto entrada)
        {
            if(entrada==null || entrada.Id<0 || entrada.CategoriaId<=0 || entrada.UnidadMedidaId<=0 || entrada.MarcaId<=0)
                throw new OperacionNoPermitidaException("Revisa el producto, su categoría, su marca opcional y su unidad de medida.");
            var p=new SolicitudProducto { Id=entrada.Id,Version=entrada.Version,Codigo=Texto(entrada.Codigo,50,"El código"),
                Descripcion=Texto(entrada.Descripcion,200,"La descripción"),CategoriaId=entrada.CategoriaId,MarcaId=entrada.MarcaId,
                UnidadMedidaId=entrada.UnidadMedidaId,PermiteDecimales=entrada.PermiteDecimales,PrecioCompra=entrada.PrecioCompra,
                PrecioVenta=entrada.PrecioVenta,StockMinimo=entrada.StockMinimo,Activo=entrada.Activo,IdNuevo=entrada.IdNuevo,
                PorcentajeIva=entrada.PorcentajeIva,GenerarCodigoBarras=entrada.GenerarCodigoBarras,
                BalanzaPesable=entrada.BalanzaPesable,BalanzaUnitario=entrada.BalanzaUnitario };
            Numero(p.PrecioCompra,99999999999999.9999m,4,"El precio de compra");
            Numero(p.PrecioVenta,9999999999999999.99m,2,"El precio de venta");
            Numero(p.StockMinimo,999999999999999.999m,3,"El stock mínimo");
            if(!p.PermiteDecimales && decimal.Truncate(p.StockMinimo)!=p.StockMinimo)
                throw new OperacionNoPermitidaException("El stock mínimo debe ser entero si el producto no permite cantidades decimales.");
            if(p.PorcentajeIva!=0 && p.PorcentajeIva!=5 && p.PorcentajeIva!=10)
                throw new OperacionNoPermitidaException("Selecciona el IVA del producto: Exento, 5 % o 10 %.");
            if(p.IdNuevo<0) throw new OperacionNoPermitidaException("Abre Nuevo producto para obtener su número.");
            int modos=(p.GenerarCodigoBarras?1:0)+(p.BalanzaPesable?1:0)+(p.BalanzaUnitario?1:0);
            if(modos>1) throw new OperacionNoPermitidaException("Selecciona una sola opción: EAN-13, pesable de balanza o unitario de balanza.");
            p.CodigoBarra=modos==1?null:Texto(entrada.CodigoBarra,100,"El código de barras");
            return p;
        }
    }
}
