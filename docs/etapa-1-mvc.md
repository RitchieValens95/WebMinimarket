# Etapa 1: pantalla inicial con ASP.NET MVC 5

La solución conserva sus cuatro proyectos y todos apuntan a .NET Framework 4.8.
MVC 5.3.0 se incorpora al proyecto Web existente junto con Razor/Web Pages 3.3.0.
Las páginas Web Forms se conservan. Esta etapa no utiliza PostgreSQL ni modifica
las capas de Entidades, Negocio o Datos.

## Cómo funciona

1. El navegador solicita `/`, `/Inicio` o `/Inicio/Index`.
2. `App_Start/RouteConfig.cs` selecciona `InicioController.Index`.
3. `Controllers/InicioController.cs` devuelve `View()`.
4. Por convención, MVC busca `Views/Inicio/Index.cshtml` y genera el HTML.
5. El navegador descarga Bootstrap desde `Content/bootstrap.min.css`.

La vista usa `Layout = null`: contiene su propio documento HTML. No usa
`Site.Master`, que pertenece a Web Forms. `Views/Web.config` configura Razor
y bloquea las solicitudes directas a los archivos de las vistas.

`Global.asax.cs` ya llama a `RouteConfig.RegisterRoutes` al iniciar la aplicación,
así que no necesita cambios para esta etapa.

## Convivencia con Web Forms

Las rutas MVC se registran antes de FriendlyUrls. Se limitan al inicio y al
prefijo `Inicio`; una ruta MVC genérica podría interceptar `/About` o `/Contact`.
FriendlyUrls conserva el acceso a esas páginas. `Default.aspx` permanece en el
proyecto y puede visitarse como `/Default`.

En `packages.config` se declaran las nuevas dependencias y en el `.csproj` se
registran sus referencias y los archivos añadidos. Los paquetes se restauran
en `packages` en la raíz de la solución; esa carpeta sigue excluida de Git.

## Validación en Visual Studio e IIS Express

Esta comprobación requiere Windows, el Developer Pack de .NET Framework 4.8
y la carga de trabajo de desarrollo web de Visual Studio. El SDK .NET 10
no sustituye estas herramientas.

Cuando estos cambios estén disponibles en tu copia de Windows:

1. Abre `D:\Sistemas\SistemaVentas\SistemaVentas.slnx`.
2. Haz clic derecho en la solución y selecciona **Restaurar paquetes NuGet**.
3. Compila la solución y verifica que termine sin errores.
4. Establece `SistemaVentas.Web` como proyecto de inicio.
5. Inicia con IIS Express. Conserva el host y puerto que abra Visual Studio y
   cambia la ruta del navegador para realizar estas comprobaciones:

| Ruta | Resultado esperado |
| --- | --- |
| `/` | Pantalla con “Bienvenido al sistema de ventas”. |
| `/Inicio` | La misma pantalla MVC. |
| `/Inicio/Index` | La misma pantalla MVC. |
| `/Content/bootstrap.min.css` | CSS disponible; la pantalla aparece con estilos. |
| `/About` y `/Contact` | Páginas Web Forms existentes. |
| `/Default` | Página Web Forms original. |
| `/Views/Inicio/Index.cshtml` | Acceso directo bloqueado, respuesta 404. |
| `/Inicio/NoExiste` | Respuesta 404 por acción inexistente. |

Si Visual Studio abre `Default.aspx` como página específica de inicio,
navega a `/Inicio/Index` para probar MVC. No es necesario borrar `Default.aspx`.

## Comprobaciones realizadas en el entorno de desarrollo

- Restauración de los paquetes declarados desde NuGet.
- Compilación directa del C# de los cuatro proyectos contra las referencias
  oficiales de .NET Framework 4.8, utilizando Roslyn. Sin errores.
- Análisis de `Index.cshtml` con Razor 3.3.0, generación de su C# y compilación
  de ese código contra las referencias de .NET Framework 4.8 y MVC 5.3.0.
  Sin errores.
- XML válido y presencia de los archivos declarados en los proyectos.

Estas comprobaciones no equivalen a compilar la solución completa con MSBuild
de Visual Studio ni ejecutan ASP.NET. No se considera validada la ejecución bajo
IIS, la carga de configuración ni las rutas HTTP hasta completar la tabla anterior
en Windows.
