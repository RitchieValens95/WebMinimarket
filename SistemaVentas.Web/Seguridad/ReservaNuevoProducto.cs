using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Security;
using SistemaVentas.Entidades;

namespace SistemaVentas.Web.Seguridad
{
    public static class ReservaNuevoProducto
    {
        public static string Crear(long id,long actor)
        {
            string contenido=id.ToString(CultureInfo.InvariantCulture)+"|"+DateTime.UtcNow.AddHours(8).Ticks.ToString(CultureInfo.InvariantCulture);
            return HttpServerUtility.UrlTokenEncode(MachineKey.Protect(Encoding.UTF8.GetBytes(contenido),"SistemaVentas.NuevoProducto.v1",actor.ToString(CultureInfo.InvariantCulture)));
        }
        public static long Leer(string token,long actor)
        {
            try
            {
                if(string.IsNullOrWhiteSpace(token) || token.Length>2048) throw new FormatException();
                byte[] bytes=HttpServerUtility.UrlTokenDecode(token);
                if(bytes==null) throw new FormatException();
                byte[] claro=MachineKey.Unprotect(bytes,"SistemaVentas.NuevoProducto.v1",actor.ToString(CultureInfo.InvariantCulture));
                if(claro==null) throw new FormatException();
                string[] campos=Encoding.UTF8.GetString(claro).Split('|');long id,vence;
                if(campos.Length!=2 || !long.TryParse(campos[0],out id) || !long.TryParse(campos[1],out vence) || id<1 || vence<DateTime.UtcNow.Ticks)
                    throw new FormatException();
                return id;
            }
            catch(Exception e) when(e is FormatException || e is CryptographicException || e is ArgumentException)
            { throw new OperacionNoPermitidaException("El formulario de nuevo producto venció o no es válido. Vuelve al listado y pulsa Nuevo producto."); }
        }
    }
}
