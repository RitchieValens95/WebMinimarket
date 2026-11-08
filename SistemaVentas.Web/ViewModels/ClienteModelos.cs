using System.ComponentModel.DataAnnotations;
using SistemaVentas.Entidades;

namespace SistemaVentas.Web.ViewModels
{
    public class ClienteFormulario
    {
        public long Id { get; set; }
        public long Version { get; set; }
        [Required(ErrorMessage="Escribe el nombre del cliente."),StringLength(150,ErrorMessage="El nombre admite hasta 150 caracteres.")]
        public string Nombre { get; set; }
        [StringLength(20),Display(Name="Tipo de documento")] public string TipoDocumento { get; set; }
        [StringLength(40,ErrorMessage="El documento admite hasta 40 caracteres."),Display(Name="Documento o RUC")]
        public string Documento { get; set; }
        [StringLength(40,ErrorMessage="El teléfono admite hasta 40 caracteres."),Display(Name="Teléfono")]
        public string Telefono { get; set; }
        [StringLength(250,ErrorMessage="La dirección admite hasta 250 caracteres."),Display(Name="Dirección")]
        public string Direccion { get; set; }
        [StringLength(254,ErrorMessage="El correo admite hasta 254 caracteres."),EmailAddress(ErrorMessage="Escribe un correo válido."),Display(Name="Correo electrónico")]
        public string Correo { get; set; }
        public bool Activo { get; set; } = true;
        public SolicitudCliente Solicitud()
        { return new SolicitudCliente{Id=Id,Version=Version,Nombre=Nombre,TipoDocumento=TipoDocumento,Documento=Documento,Telefono=Telefono,Direccion=Direccion,Correo=Correo,Activo=Activo}; }
        public static ClienteFormulario Desde(Cliente c)
        { return new ClienteFormulario{Id=c.Id,Version=c.Version,Nombre=c.Nombre,TipoDocumento=c.TipoDocumento,Documento=c.Documento,Telefono=c.Telefono,Direccion=c.Direccion,Correo=c.Correo,Activo=c.Activo}; }
    }
}
