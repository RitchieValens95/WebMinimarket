using System;
using System.Web;
using System.Web.Caching;

namespace SistemaVentas.Web.Seguridad
{
    public static class LimiteAccesos
    {
        private sealed class Contador { public int Intentos; }
        private static readonly object Cerrojo = new object();
        public static bool Permitir(string direccion)
        {
            var clave = "SistemaVentas.Accesos:" + (direccion ?? "local");
            lock (Cerrojo)
            {
                var contador = HttpRuntime.Cache[clave] as Contador;
                if (contador == null)
                {
                    contador = new Contador();
                    HttpRuntime.Cache.Insert(clave, contador, null, DateTime.UtcNow.AddMinutes(1), Cache.NoSlidingExpiration);
                }
                contador.Intentos++;
                return contador.Intentos <= 20;
            }
        }
    }
}
