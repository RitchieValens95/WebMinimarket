using System.Globalization;
using SistemaVentas.Entidades;

namespace SistemaVentas.Negocio
{
    public static class CodigoBalanza
    {
        public static string Prefijo(long plu,bool unitario)
        {
            if(plu<1 || plu>99999)
                throw new OperacionNoPermitidaException("Para códigos de balanza, el número de producto debe estar entre 1 y 99999. No se pueden recortar sus dígitos.");
            return (unitario?"21":"20")+plu.ToString("D5",CultureInfo.InvariantCulture);
        }
        public static string Etiqueta(long plu,bool unitario,int valorCodificado)
        {
            if(valorCodificado<0 || valorCodificado>99999)
                throw new OperacionNoPermitidaException("El peso o cantidad codificado debe caber en cinco dígitos.");
            return Ean13.Completar(Prefijo(plu,unitario)+valorCodificado.ToString("D5",CultureInfo.InvariantCulture));
        }
        public static LecturaBalanza Leer(string etiqueta)
        {
            if(!Ean13.EsValido(etiqueta) || (!etiqueta.StartsWith("20") && !etiqueta.StartsWith("21"))) return null;
            long plu=long.Parse(etiqueta.Substring(2,5),CultureInfo.InvariantCulture);
            if(plu==0) return null;
            return new LecturaBalanza { Plu=plu,EsUnitario=etiqueta.StartsWith("21"),Prefijo=etiqueta.Substring(0,7),ValorCodificado=etiqueta.Substring(7,5) };
        }
    }
}
