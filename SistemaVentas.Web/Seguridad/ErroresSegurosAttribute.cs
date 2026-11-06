using System;
using System.Web.Mvc;

namespace SistemaVentas.Web.Seguridad
{
    public sealed class ErroresSegurosAttribute : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext contexto)
        {
            if (contexto.ExceptionHandled) return;
            bool csrf = contexto.Exception is HttpAntiForgeryException;
            System.Diagnostics.Trace.TraceError("Error de solicitud: {0}", contexto.Exception.GetType().Name);
            contexto.ExceptionHandled = true;
            contexto.HttpContext.Response.StatusCode = csrf ? 400 : 500;
            contexto.HttpContext.Response.TrySkipIisCustomErrors = true;
            contexto.HttpContext.Response.SuppressFormsAuthenticationRedirect = true;
            contexto.Result = new ContentResult { ContentType = "text/plain; charset=utf-8", Content = csrf
                ? "El formulario venció o no es válido. Recarga la página e inténtalo nuevamente."
                : "No se pudo completar la operación. Intenta nuevamente o contacta al administrador." };
        }
    }
}
