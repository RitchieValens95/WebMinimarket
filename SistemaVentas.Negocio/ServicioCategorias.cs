using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public sealed class ServicioCategorias : ServicioCatalogo
    {
        public ServicioCategorias(string cadena) : base(cadena,TipoCatalogo.Categorias) { }
    }
}
