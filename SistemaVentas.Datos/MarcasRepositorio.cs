using SistemaVentas.Entidades;

namespace SistemaVentas.Datos
{
    public sealed class MarcasRepositorio : RepositorioCatalogo
    {
        public MarcasRepositorio(string cadena) : base(cadena,TipoCatalogo.Marcas) { }
    }
}
