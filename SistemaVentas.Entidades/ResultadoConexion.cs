namespace SistemaVentas.Entidades
{
    public class ResultadoConexion
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
        public string VersionServidor { get; set; }
        public int VersionPrincipal { get; set; }
    }
}
