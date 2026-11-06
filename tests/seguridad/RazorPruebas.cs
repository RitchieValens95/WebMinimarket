using System;
using System.IO;
using System.Web.Mvc;
using System.Web.Razor;
using Microsoft.CSharp;

internal static class RazorPruebas
{
    private static int Main(string[] args)
    {
        int cantidad=0;
        foreach(var path in Directory.GetFiles("/app/Views","*.cshtml",SearchOption.AllDirectories))
        {
            var virtualPath="~/"+path.Substring("/app/".Length);
            var host=new MvcWebRazorHostFactory().CreateHost(virtualPath,path);
            foreach(var ns in new[]{"System","System.Linq","System.Web.Mvc","System.Web.Mvc.Html","System.Web.Routing","SistemaVentas.Web"})host.NamespaceImports.Add(ns);
            using(var input=File.OpenText(path))
            {
                var result=new RazorTemplateEngine(host).GenerateCode(input,"Vista"+cantidad,"Validacion",path);
                foreach(var error in result.ParserErrors)Console.Error.WriteLine(path+": "+error);
                if(!result.Success)return 1;
                using(var output=File.CreateText("/runtime/Vista"+cantidad+".cs"))
                    new CSharpCodeProvider().GenerateCodeFromCompileUnit(result.GeneratedCode,output,new System.CodeDom.Compiler.CodeGeneratorOptions());
            }
            cantidad++;
        }
        Console.WriteLine(cantidad+" vistas procesadas con el host MVC 5.3.0.");return 0;
    }
}
