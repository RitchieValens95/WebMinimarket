using System;
using System.Collections.Generic;

namespace SistemaVentas.Entidades
{
    public class SolicitudCliente
    {
        public long Id { get; set; }
        public long Version { get; set; }
        public string Nombre { get; set; }
        public string TipoDocumento { get; set; }
        public string Documento { get; set; }
        public string Telefono { get; set; }
        public string Direccion { get; set; }
        public string Correo { get; set; }
        public bool Activo { get; set; } = true;
    }
    public class Cliente : SolicitudCliente
    {
        // Este indicador se lee de la base; nunca se recibe en una escritura.
        public bool EsGenerico { get; set; }
    }
    public class PaginaClientes
    {
        public List<Cliente> Registros { get; set; } = new List<Cliente>();
        public int Pagina { get; set; }
        public long Total { get; set; }
        public string Busqueda { get; set; }
        public string Estado { get; set; }
        public int Tamano { get { return 20; } }
    }
    public class CompraCliente
    {
        public long Numero { get; set; }
        public DateTimeOffset Fecha { get; set; }
        public decimal Total { get; set; }
        public string Vendedor { get; set; }
    }
    public class HistorialCliente
    {
        public Cliente Cliente { get; set; }
        public List<CompraCliente> Compras { get; set; } = new List<CompraCliente>();
        public int Pagina { get; set; }
        public long Total { get; set; }
        public decimal ImporteTotal { get; set; }
        public bool SoloPropias { get; set; }
    }
}
