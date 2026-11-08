using System;
using System.Collections.Generic;

namespace SistemaVentas.Entidades
{
    public class SolicitudProveedor
    {
        public long Id { get; set; }
        public long Version { get; set; }
        public string Nombre { get; set; }
        public string Ruc { get; set; }
        public string Telefono { get; set; }
        public string Direccion { get; set; }
        public string Correo { get; set; }
        public bool Activo { get; set; } = true;
    }
    public class Proveedor : SolicitudProveedor { }
    public class PaginaProveedores
    {
        public List<Proveedor> Registros { get; set; } = new List<Proveedor>();
        public int Pagina { get; set; }
        public long Total { get; set; }
        public string Busqueda { get; set; }
        public string Estado { get; set; }
        public int Tamano { get { return 20; } }
    }
    public class CompraProveedor
    {
        public long Id { get; set; }
        public string NumeroDocumento { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public decimal Total { get; set; }
        public string Usuario { get; set; }
    }
    public class HistorialProveedor
    {
        public Proveedor Proveedor { get; set; }
        public List<CompraProveedor> Compras { get; set; } = new List<CompraProveedor>();
        public int Pagina { get; set; }
        public long Total { get; set; }
        public decimal ImporteTotal { get; set; }
    }
}
