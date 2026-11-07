using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public sealed class ServicioMarcas : ServicioCatalogo
    {
        public ServicioMarcas(string cadena) : base(cadena,TipoCatalogo.Marcas) { }
    }
}
