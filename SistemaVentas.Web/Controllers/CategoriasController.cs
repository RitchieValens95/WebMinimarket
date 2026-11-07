using SistemaVentas.Negocio;
using SistemaVentas.Web.Seguridad;

namespace SistemaVentas.Web.Controllers
{
    [Permiso("CATEGORIAS_ADMINISTRAR")]
    public class CategoriasController : CatalogosControllerBase
    {
        protected override ServicioCatalogo Servicio { get { return Aplicacion.Categorias(); } }
        protected override string NombreCatalogo { get { return "Categorías"; } }
    }
}
