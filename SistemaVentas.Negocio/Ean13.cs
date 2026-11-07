using System;
using System.Globalization;
using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public static class Ean13
    {
        public static string Generar(long idProducto)
        {
            if(idProducto<1 || idProducto>9999999999L)
                throw new OperacionNoPermitidaException("El número de producto debe tener entre 1 y 10 dígitos para generar su EAN-13.");
            string baseDoce="28"+idProducto.ToString("D10",CultureInfo.InvariantCulture);
            return baseDoce+Control(baseDoce).ToString(CultureInfo.InvariantCulture);
        }
        private static int Control(string baseDoce)
        {
            int suma=0;
            for(int i=0;i<12;i++) suma+=(baseDoce[i]-'0')*(i%2==0?1:3);
            return (10-suma%10)%10;
        }
        public static bool EsValido(string codigo)
        {
            if(codigo==null || codigo.Length!=13) return false;
            foreach(char c in codigo) if(c<'0' || c>'9') return false;
            return codigo[12]-'0'==Control(codigo.Substring(0,12));
        }
    }
}
