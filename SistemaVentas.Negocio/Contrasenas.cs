using System;
using System.Security.Cryptography;

namespace SistemaVentas.Negocio
{
    public static class Contrasenas
    {
        public const int Iteraciones = 600000;

        public static string Crear(string password)
        {
            if (password == null) throw new ArgumentNullException("password");
            var sal = new byte[16];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(sal);
            using (var derivador = new Rfc2898DeriveBytes(password, sal, Iteraciones, HashAlgorithmName.SHA256))
            {
                return "v1$pbkdf2-sha256$" + Iteraciones + "$" + Convert.ToBase64String(sal) + "$" + Convert.ToBase64String(derivador.GetBytes(32));
            }
        }

        public static bool Verificar(string password, string hash)
        {
            if (password == null || password.Length > 128 || string.IsNullOrEmpty(hash)) return false;
            var partes = hash.Split('$');
            int iteraciones;
            if (partes.Length != 5 || partes[0] != "v1" || partes[1] != "pbkdf2-sha256" ||
                !int.TryParse(partes[2], out iteraciones) || iteraciones < Iteraciones || iteraciones > 2000000) return false;
            try
            {
                var sal = Convert.FromBase64String(partes[3]);
                var esperado = Convert.FromBase64String(partes[4]);
                if (sal.Length != 16 || esperado.Length != 32) return false;
                using (var derivador = new Rfc2898DeriveBytes(password, sal, iteraciones, HashAlgorithmName.SHA256))
                {
                    var calculado = derivador.GetBytes(32);
                    int diferencia = 0;
                    for (int i = 0; i < esperado.Length; i++) diferencia |= esperado[i] ^ calculado[i];
                    Array.Clear(calculado, 0, calculado.Length);
                    return diferencia == 0;
                }
            }
            catch (FormatException) { return false; }
        }
    }
}
