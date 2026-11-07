using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using SistemaVentas.Entidades;

namespace SistemaVentas.Web.ViewModels
{
    public class ProductoFormulario
    {
        public long Id { get; set; }
        public long Version { get; set; }
        [Required(ErrorMessage="Escribe el código."),StringLength(50,ErrorMessage="El código admite hasta 50 caracteres."),Display(Name="Código interno")]
        public string Codigo { get; set; }
        [Required(ErrorMessage="Escribe la descripción."),StringLength(200,ErrorMessage="La descripción admite hasta 200 caracteres."),Display(Name="Descripción")]
        public string Descripcion { get; set; }
        [Range(1,long.MaxValue,ErrorMessage="Selecciona una categoría."),Display(Name="Categoría")]
        public long CategoriaId { get; set; }
        [Display(Name="Marca")] public long? MarcaId { get; set; }
        [Range(1,long.MaxValue,ErrorMessage="Selecciona una unidad de medida."),Display(Name="Unidad de medida")]
        public long UnidadMedidaId { get; set; }
        [Display(Name="Permite cantidades decimales")] public bool PermiteDecimales { get; set; }
        // El texto permite reconocer coma o punto sin depender del idioma de Windows.
        [Required(ErrorMessage="Escribe el precio de compra."),StringLength(32,ErrorMessage="El número admite hasta 32 caracteres."),Display(Name="Precio de compra")]
        public string PrecioCompra { get; set; } = "0";
        [Required(ErrorMessage="Escribe el precio de venta."),StringLength(32,ErrorMessage="El número admite hasta 32 caracteres."),Display(Name="Precio de venta")]
        public string PrecioVenta { get; set; } = "0";
        [Required(ErrorMessage="Escribe el stock mínimo."),StringLength(32,ErrorMessage="El número admite hasta 32 caracteres."),Display(Name="Stock mínimo")]
        public string StockMinimo { get; set; } = "0";
        public bool Activo { get; set; } = true;
        [StringLength(100,ErrorMessage="El código de barras admite hasta 100 caracteres."),Display(Name="Código de barras")]
        public string CodigoBarra { get; set; }
        [Display(Name="Sin código de fabricante: generar EAN-13")]
        public bool GenerarCodigoBarras { get; set; }
        [Display(Name="Pesable de balanza (20)")] public bool BalanzaPesable { get; set; }
        [Display(Name="Unitario de balanza (21)")] public bool BalanzaUnitario { get; set; }
        [Required(ErrorMessage="Selecciona el IVA del producto."),Display(Name="IVA incluido en los precios")]
        public int? PorcentajeIva { get; set; }
        [StringLength(2048)] public string Reserva { get; set; }
        public long IdNuevo { get; set; }
        public string CodigoGenerado { get; set; }
        public string CodigoPesable { get; set; }
        public string CodigoUnitario { get; set; }
        public decimal StockActual { get; set; }
        public OpcionesProducto Opciones { get; set; } = new OpcionesProducto();

        public SolicitudProducto Solicitud(ModelStateDictionary estado)
        {
            decimal compra,venta,minimo;
            if(!Numero(PrecioCompra,out compra)) estado.AddModelError("PrecioCompra","Escribe un número sin separadores de miles; puedes usar coma o punto decimal.");
            if(!Numero(PrecioVenta,out venta)) estado.AddModelError("PrecioVenta","Escribe un número sin separadores de miles; puedes usar coma o punto decimal.");
            if(!Numero(StockMinimo,out minimo)) estado.AddModelError("StockMinimo","Escribe un número sin separadores de miles; puedes usar coma o punto decimal.");
            return new SolicitudProducto { Id=Id,Version=Version,Codigo=Codigo,Descripcion=Descripcion,CategoriaId=CategoriaId,MarcaId=MarcaId,
                UnidadMedidaId=UnidadMedidaId,PermiteDecimales=PermiteDecimales,PrecioCompra=compra,PrecioVenta=venta,StockMinimo=minimo,Activo=Activo,
                CodigoBarra=CodigoBarra,GenerarCodigoBarras=GenerarCodigoBarras,BalanzaPesable=BalanzaPesable,BalanzaUnitario=BalanzaUnitario,PorcentajeIva=PorcentajeIva,IdNuevo=IdNuevo };
        }
        private static bool Numero(string texto,out decimal valor)
        { return decimal.TryParse((texto??"").Trim().Replace(',','.'),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out valor); }
        public static ProductoFormulario Desde(Producto p)
        {
            return new ProductoFormulario { Id=p.Id,Version=p.Version,Codigo=p.Codigo,Descripcion=p.Descripcion,CategoriaId=p.CategoriaId,MarcaId=p.MarcaId,
                UnidadMedidaId=p.UnidadMedidaId,PermiteDecimales=p.PermiteDecimales,PrecioCompra=p.PrecioCompra.ToString(CultureInfo.InvariantCulture),
                PrecioVenta=p.PrecioVenta.ToString(CultureInfo.InvariantCulture),StockMinimo=p.StockMinimo.ToString(CultureInfo.InvariantCulture),
                Activo=p.Activo,CodigoBarra=p.CodigoBarra,GenerarCodigoBarras=p.GenerarCodigoBarras || (string.IsNullOrEmpty(p.CodigoBarra) && !p.BalanzaPesable && !p.BalanzaUnitario),
                BalanzaPesable=p.BalanzaPesable,BalanzaUnitario=p.BalanzaUnitario,
                PorcentajeIva=p.PorcentajeIva,StockActual=p.StockActual };
        }
    }
    public class ProductosListado
    {
        public PaginaProductos Datos { get; set; }
        public OpcionesProducto Opciones { get; set; }
    }
}
