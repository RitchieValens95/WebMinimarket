using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SistemaVentas.Entidades;

namespace SistemaVentas.Web.ViewModels
{
    public class IngresoModelo
    {
        [Required(ErrorMessage = "Escribe tu usuario."), StringLength(80), Display(Name = "Usuario")]
        public string Usuario { get; set; }
        [Required(ErrorMessage = "Escribe tu contraseña."), StringLength(128), DataType(DataType.Password), Display(Name = "Contraseña")]
        public string Password { get; set; }
        [StringLength(2048)] public string ReturnUrl { get; set; }
    }

    public class UsuarioFormulario
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Escribe el usuario."), RegularExpression("^[A-Za-z0-9_.-]{3,80}$", ErrorMessage = "Usa de 3 a 80 letras sin acentos, números, puntos o guiones."), Display(Name = "Usuario")]
        public string NombreUsuario { get; set; }
        [Required(ErrorMessage = "Escribe el nombre."), StringLength(150, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 150 caracteres.")] public string Nombre { get; set; }
        [StringLength(254, ErrorMessage = "El correo no puede superar 254 caracteres."), EmailAddress(ErrorMessage = "Escribe un correo válido.")] public string Correo { get; set; }
        [StringLength(128, MinimumLength = 12, ErrorMessage = "La contraseña debe tener entre 12 y 128 caracteres."), DataType(DataType.Password), Display(Name = "Contraseña nueva")]
        public string Password { get; set; }
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden."), DataType(DataType.Password), Display(Name = "Repetir contraseña")]
        public string Confirmacion { get; set; }
        public bool Activo { get; set; } = true;
        public long[] RolIds { get; set; } = new long[0];
        public List<RolSeguridad> RolesDisponibles { get; set; } = new List<RolSeguridad>();
    }

    public class RolFormulario
    {
        public long Id { get; set; }
        [Required(ErrorMessage = "Escribe el código."), RegularExpression("^[A-Z][A-Z0-9_]{1,39}$", ErrorMessage = "Usa de 2 a 40 letras mayúsculas, números o guiones bajos."), Display(Name = "Código")]
        public string Codigo { get; set; }
        [Required(ErrorMessage = "Escribe el nombre del rol."), StringLength(80, MinimumLength = 2, ErrorMessage = "El nombre del rol debe tener entre 2 y 80 caracteres.")] public string Nombre { get; set; }
        public bool Activo { get; set; } = true;
        public long[] PermisoIds { get; set; } = new long[0];
        public List<PermisoSeguridad> PermisosDisponibles { get; set; } = new List<PermisoSeguridad>();
    }

    public class RolesListado
    {
        public List<RolSeguridad> Roles { get; set; }
        public int Total { get; set; }
        public int Pagina { get; set; }
        public string Busqueda { get; set; }
    }
}
