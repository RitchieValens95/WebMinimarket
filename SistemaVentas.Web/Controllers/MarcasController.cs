using SistemaVentas.Negocio;
using SistemaVentas.Web.Seguridad;

namespace SistemaVentas.Web.Controllers
{
    [Permiso("MARCAS_ADMINISTRAR")]
    public class MarcasController : CatalogosControllerBase
    {
        protected override ServicioCatalogo Servicio { get { return Aplicacion.Marcas(); } }
        protected override string NombreCatalogo { get { return "Marcas"; } }
    }
}
