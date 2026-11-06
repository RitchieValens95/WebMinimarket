using System;
using System.IO;
using System.Text;
using System.Xml;
using SistemaVentas.Entidades;
using SistemaVentas.Negocio;

internal static class AdministradorInicial
{
    private static string Password(string titulo)
    {
        Console.Write(titulo);
        var texto = new StringBuilder();
        for (;;)
        {
            var tecla = Console.ReadKey(true);
            if (tecla.Key == ConsoleKey.Enter) { Console.WriteLine(); return texto.ToString(); }
            if (tecla.Key == ConsoleKey.Backspace && texto.Length > 0) texto.Length--;
            else if (!char.IsControl(tecla.KeyChar))
            {
                if (texto.Length >= 128) throw new OperacionNoPermitidaException("La contraseña no puede superar 128 caracteres.");
                texto.Append(tecla.KeyChar);
            }
        }
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 1) throw new OperacionNoPermitidaException("Indica la ruta de Web.config.");
            var configuracion = new XmlDocument { XmlResolver = null };
            configuracion.Load(args[0]);
            var entrada = configuracion.SelectSingleNode("/configuration/appSettings/add[@key='SistemaVentas.PostgreSql']/@value");
            var archivo = configuracion.SelectSingleNode("/configuration/appSettings/@file");
            if (archivo != null)
            {
                var ruta = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[0])), archivo.Value);
                if (File.Exists(ruta))
                {
                    var local = new XmlDocument { XmlResolver = null }; local.Load(ruta);
                    entrada = local.SelectSingleNode("/appSettings/add[@key='SistemaVentas.PostgreSql']/@value") ?? entrada;
                }
            }
            if (entrada == null || string.IsNullOrWhiteSpace(entrada.Value))
                throw new OperacionNoPermitidaException("Primero configura AppSettings.Local.config en el proyecto Web.");
            Console.Write("Usuario del administrador (por ejemplo admin): ");
            var usuario = Console.ReadLine();
            Console.Write("Nombre de la persona: ");
            var nombre = Console.ReadLine();
            var password = Password("Contraseña nueva (mínimo 12 caracteres; no se mostrará): ");
            var confirmacion = Password("Repite la contraseña: ");
            if (password != confirmacion) throw new OperacionNoPermitidaException("Las contraseñas no coinciden.");
            var id = new ServicioSeguridad(entrada.Value).CrearPrimerAdministrador(usuario, nombre, password);
            Console.WriteLine("Administrador creado. Id: " + id + ". Inicia sesión con el usuario que elegiste.");
            return 0;
        }
        catch (OperacionNoPermitidaException e) { Console.Error.WriteLine(e.Message); return 1; }
        catch { Console.Error.WriteLine("No se pudo crear el administrador. Comprueba la compilación, la conexión y la migración 004."); return 1; }
    }
}
