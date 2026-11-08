using System.ComponentModel.DataAnnotations;
using SistemaVentas.Entidades;

namespace SistemaVentas.Web.ViewModels
{
    public class ProveedorFormulario
    {
        public long Id { get; set; }
        public long Version { get; set; }
        [Required(ErrorMessage="Escribe el nombre o razón social."),StringLength(150,ErrorMessage="El nombre admite hasta 150 caracteres."),Display(Name="Nombre o razón social")]
        public string Nombre { get; set; }
        [StringLength(40,ErrorMessage="El RUC admite hasta 40 caracteres."),Display(Name="RUC")]
        public string Ruc { get; set; }
        [StringLength(40,ErrorMessage="El teléfono admite hasta 40 caracteres."),Display(Name="Teléfono")]
        public string Telefono { get; set; }
        [StringLength(250,ErrorMessage="La dirección admite hasta 250 caracteres."),Display(Name="Dirección")]
        public string Direccion { get; set; }
        [StringLength(254,ErrorMessage="El correo admite hasta 254 caracteres."),EmailAddress(ErrorMessage="Escribe un correo válido."),Display(Name="Correo electrónico")]
        public string Correo { get; set; }
        public bool Activo { get; set; } = true;
        public SolicitudProveedor Solicitud()
        {return new SolicitudProveedor{Id=Id,Version=Version,Nombre=Nombre,Ruc=Ruc,Telefono=Telefono,Direccion=Direccion,Correo=Correo,Activo=Activo};}
        public static ProveedorFormulario Desde(Proveedor p)
        {return new ProveedorFormulario{Id=p.Id,Version=p.Version,Nombre=p.Nombre,Ruc=p.Ruc,Telefono=p.Telefono,Direccion=p.Direccion,Correo=p.Correo,Activo=p.Activo};}
    }
}
