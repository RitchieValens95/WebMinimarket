using SistemaVentas.Entidades;

namespace SistemaVentas.Datos
{
    public sealed class CategoriasRepositorio : RepositorioCatalogo
    {
        public CategoriasRepositorio(string cadena) : base(cadena,TipoCatalogo.Categorias) { }
    }
}
