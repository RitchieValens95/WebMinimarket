using System;
using System.Collections.Generic;

namespace SistemaVentas.Entidades
{
    public class SolicitudProducto
    {
        public long Id { get; set; }
        public long Version { get; set; }
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public long CategoriaId { get; set; }
        public long? MarcaId { get; set; }
        public long UnidadMedidaId { get; set; }
        public bool PermiteDecimales { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal StockMinimo { get; set; }
        public bool Activo { get; set; } = true;
        public List<string> CodigosBarras { get; set; } = new List<string>();
    }

    public class Producto : SolicitudProducto
    {
        // StockActual es informativo; no forma parte de la solicitud de escritura.
        public decimal StockActual { get; set; }
        public string Categoria { get; set; }
        public string Marca { get; set; }
        public string UnidadMedida { get; set; }
    }

    public class OpcionProducto
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
    }

    public class OpcionesProducto
    {
        public List<OpcionProducto> Categorias { get; set; } = new List<OpcionProducto>();
        public List<OpcionProducto> Marcas { get; set; } = new List<OpcionProducto>();
        public List<OpcionProducto> Unidades { get; set; } = new List<OpcionProducto>();
    }

    public class FiltroProductos
    {
        public int Pagina { get; set; } = 1;
        public string Busqueda { get; set; } = "";
        public string Estado { get; set; } = "todos";
        public long? CategoriaId { get; set; }
        public long? MarcaId { get; set; }
        public string Stock { get; set; } = "todos";
    }

    public class PaginaProductos
    {
        public List<Producto> Registros { get; set; } = new List<Producto>();
        public FiltroProductos Filtro { get; set; }
        public long Total { get; set; }
        public int Tamano { get { return 20; } }
    }

    public class MovimientoProducto
    {
        public long Id { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public string Tipo { get; set; }
        public string TipoDescripcion
        {
            get
            {
                switch(Tipo)
                {
                    case "SALDO_INICIAL": return "Saldo inicial";
                    case "ENTRADA_COMPRA": return "Entrada por compra";
                    case "SALIDA_VENTA": return "Salida por venta";
                    case "AJUSTE_ENTRADA": return "Ajuste de entrada";
                    case "AJUSTE_SALIDA": return "Ajuste de salida";
                    default: return "Movimiento";
                }
            }
        }
        public decimal Cantidad { get; set; }
        public decimal SaldoAnterior { get; set; }
        public decimal SaldoPosterior { get; set; }
        public decimal CostoUnitario { get; set; }
        public string Usuario { get; set; }
        public string Observacion { get; set; }
    }

    public class HistorialProducto
    {
        public Producto Producto { get; set; }
        public List<MovimientoProducto> Movimientos { get; set; } = new List<MovimientoProducto>();
        public int Pagina { get; set; }
        public long Total { get; set; }
    }
}
