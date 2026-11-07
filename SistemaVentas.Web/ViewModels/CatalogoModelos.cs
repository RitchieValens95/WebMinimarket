using System.ComponentModel.DataAnnotations;
using SistemaVentas.Entidades;

namespace SistemaVentas.Web.ViewModels
{
    public class CatalogoFormulario
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Escribe el nombre."), StringLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres.")]
        public string Nombre { get; set; }
        public bool Activo { get; set; } = true;
        public long Version { get; set; }
    }

    public class CatalogoListado
    {
        public PaginaCatalogo Datos { get; set; }
    }
}
