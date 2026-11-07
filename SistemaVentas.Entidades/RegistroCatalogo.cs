using System.Collections.Generic;

namespace SistemaVentas.Entidades
{
    public enum TipoCatalogo { Categorias = 1, Marcas = 2 }

    public abstract class RegistroCatalogo
    {
        public long Id { get; set; }
        public string Nombre { get; set; }
        public bool Activo { get; set; } = true;
        public long Version { get; set; }
    }

    public class PaginaCatalogo
    {
        public List<RegistroCatalogo> Registros { get; set; }
        public long Total { get; set; }
        public int Pagina { get; set; }
        public string Busqueda { get; set; }
        public string Estado { get; set; }
        public int Tamano { get { return 20; } }
    }
}
