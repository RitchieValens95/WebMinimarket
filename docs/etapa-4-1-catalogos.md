# Etapa 4.1: categorías y marcas

Esta primera parte de la etapa 4 permite crear, consultar, buscar, editar,
desactivar y reactivar categorías y marcas. Conserva los cuatro proyectos,
.NET Framework 4.8, ASP.NET MVC 5, Bootstrap 5 y PostgreSQL 18.
Productos, clientes y proveedores se desarrollarán en las siguientes partes,
con explicación y confirmación antes de comenzar cada una.

## Cómo se relacionan los archivos

El recorrido de una solicitud es:

```text
Navegador → ruta MVC → controlador → servicio → repositorio → PostgreSQL
                            ↓
                         vista Razor → HTML para el navegador
```

- **Entidades:** `Categoria.cs` y `Marca.cs` representan registros. Comparten
  `RegistroCatalogo.cs`: identificador, nombre, estado y versión; `PaginaCatalogo`
  transporta los resultados de una búsqueda.
- **Datos:** `CategoriasRepositorio.cs` y `MarcasRepositorio.cs` seleccionan su
  tabla mediante una lista cerrada. `RepositorioCatalogo.cs` ejecuta consultas
  con parámetros, transacciones y auditoría. Ninguna petición elige una tabla SQL.
- **Negocio:** `ServicioCategorias.cs` y `ServicioMarcas.cs` reutilizan
  `ServicioCatalogo.cs`: verifica permisos en cada operación, valida el nombre
  y comprueba la versión antes de guardar.
- **Web:** `CategoriasController.cs` y `MarcasController.cs` exigen sus permisos.
  Heredan las acciones de `CatalogosControllerBase.cs`. `CatalogoModelos.cs`
  describe los campos admitidos por los formularios. Las vistas
  `Views/Catalogos/Index.cshtml` y `Formulario.cshtml` comparten la presentación
  de ambos catálogos. Razor codifica el texto que se muestra.
- **Conexión y navegación:** `Seguridad/Aplicacion.cs` crea los servicios con la
  conexión privada existente; `App_Start/RouteConfig.cs` registra las rutas MVC
  antes de FriendlyUrls. `Views/Shared/_Layout.cshtml` muestra los enlaces según
  los permisos del usuario.
- **Base:** `006_catalogos.sql` incorpora las relaciones que protegen los
  productos activos; `007_comprobar_catalogos.sql` verifica esa estructura.

No se eliminan proyectos, tablas, registros ni páginas anteriores. Los archivos
nuevos están incluidos explícitamente en los `.csproj`; no se agregan paquetes
NuGet. Las vistas usan UTF-8 con BOM para conservar las tildes en Visual Studio.

## Reglas

El nombre se recorta y normaliza a Unicode NFC, admite de 1 a 100 caracteres
y rechaza saltos de línea y caracteres de control. Un nombre no puede repetirse
ignorando mayúsculas y espacios al principio/final, aunque el registro existente
esté inactivo. En ese caso se edita o reactiva el registro existente.

La búsqueda y los filtros Todos/Activos/Inactivos consultan PostgreSQL, con
páginas de 20 registros. Una búsqueda vacía muestra todos; `%` y `_` conservan
su significado de comodines de PostgreSQL. Los parámetros evitan que el texto de
búsqueda se interprete como instrucciones SQL.

Desactivar conserva el identificador y las relaciones. Solo se permite si no
hay productos **activos** asociados. Los productos inactivos conservan su
categoría y marca. La marca sigue siendo opcional. Un producto no puede
activarse mientras su categoría o su marca asignada estén inactivas.

La migración agrega claves únicas `(id, activo)` a categorías y marcas, una
columna generada en productos y dos claves foráneas compuestas. PostgreSQL
aplica esta regla también a SQL directo y a escrituras simultáneas. Las claves
foráneas originales siguen conservando las relaciones de los productos inactivos.

La versión del formulario proviene de `xmin`, el identificador de la transacción
que modificó la fila en PostgreSQL. Si otra ventana guardó primero, se rechaza el
formulario antiguo. Vuelve al listado y abre Editar para obtener la versión actual.
Las modificaciones confirmadas registran actor, acción e identificador en auditoría
dentro de la misma transacción. Guardar sin cambios no crea una modificación.

Administrador y Supervisor tienen inicialmente `CATEGORIAS_ADMINISTRAR` y
`MARCAS_ADMINISTRAR`. Cajero no los tiene. Ocultar el menú se complementa con
la validación en el controlador y en el servicio; una URL directa sin permiso
recibe 403. Guardar y Desactivar son POST con token antifalsificación.

## Aplicar en tu notebook cuando esta entrega esté disponible

1. Detén IIS Express y conserva tu configuración privada de conexión. Actualiza
   el código a la rama de esta entrega con el procedimiento indicado en el chat.
   No sobrescribas archivos locales si Git informa cambios pendientes.
2. En pgAdmin abre el Query Tool de **tu base del sistema**, con el propietario
   de la base. Debe estar validada la etapa 3. No selecciones `postgres`,
   `template0` ni `template1`.
3. Abre y ejecuta **solo** `database/006_catalogos.sql`. Debe terminar con
   `Migración 006 aplicada correctamente.` y `COMMIT`. Repetir exactamente el
   mismo archivo informa que ya está aplicada; modificar una versión aplicada
   provoca un rechazo por checksum.
4. Si indica que existen productos activos con categoría o marca inactiva, la
   migración revierte completamente. Revisa esos registros y decide cuáles
   reactivar o desactivar antes de repetir. El script no cambia sus datos por ti.
   Si informa un bloqueo o tiempo de espera, detén las otras operaciones sobre
   la base y repite el mismo archivo; no modifiques el checksum ni las versiones.
5. Ejecuta `database/007_comprobar_catalogos.sql`. Debe mostrar
   `Categorías, marcas y relaciones de productos verificadas correctamente.`
   También puedes ejecutar `003_comprobar_estructura.sql`, ahora compatible
   tanto con la etapa anterior como con la migración 006.
6. En Visual Studio restaura NuGet si lo solicita, recompila la solución e inicia
   por **HTTPS**. Los servicios de escritura requieren la versión 006 registrada.
   La conexión local y las contraseñas se mantienen fuera de Git.

**No vuelvas a ejecutar 001, 002 ni 004 para esta actualización.** No necesitas
recrear la base ni crear otra cuenta de administrador. 007 es de solo lectura.
La migración no cambia precios, existencias, ventas ni credenciales.

## Recorrido de comprobación en Windows

1. Ingresa como administrador. Abre **Categorías**, crea `Bebidas`, busca `beb`
   y comprueba el resultado. Edita el nombre y guarda. Repite en **Marcas**.
2. Intenta crear el mismo nombre en mayúsculas: debe rechazarlo sin duplicar datos.
3. Desactiva un registro sin productos activos. Filtra Inactivos; abre Editar,
   marca Activo y guarda. Debe conservar su identificador.
4. Abre el mismo registro para editar en dos pestañas. Guarda un cambio en la
   primera e intenta guardar en la segunda. Debe pedir recargar sin sobrescribir.
5. Si tienes productos reales de demostración, intenta desactivar su categoría
   o marca: debe rechazarlo mientras haya productos activos asociados.
6. Prueba los filtros y las páginas si hay más de 20 registros. Comprueba tildes
   y que el menú y los botones se vean en una ventana estrecha.
7. Con una cuenta Cajero sin permisos adicionales, los enlaces no aparecen y las
   URL `/Categorias` y `/Marcas` deben responder 403. Un Supervisor con los
   permisos iniciales puede administrar ambos catálogos.
8. Comprueba que Inicio, Usuarios, Roles y Cerrar sesión siguen funcionando.

## Validación automatizada y límites

`tests/catalogos/CatalogosPruebas.cs` ejecuta las operaciones de los servicios
contra PostgreSQL 18 real en una base temporal. Incluye nombres Unicode,
duplicados, filtros, páginas, permisos, auditoría, cambios obsoletos, claves
foráneas y carreras entre edición, inserción de producto y desactivación.
El ejecutor también comprueba el rechazo transaccional de datos previos
incompatibles, la repetición y el checksum de la migración, y compila
los cuatro proyectos y todas las vistas con MVC 5/Razor 3 y referencias net48.

```bash
python3 tests/seguridad/ejecutar.py --catalogos \
  --dotnet /ruta/al/dotnet \
  --referencias /ruta/al/paquete/net48/build/.NETFramework/v4.8
```

Docker debe tener `postgres:18` y `mono:6.12`; los paquetes de `packages/` deben
estar restaurados. Las credenciales temporales se generan durante la prueba y
los contenedores se eliminan al finalizar. No se conecta a tu base. Sin
`--catalogos`, el mismo ejecutor comprueba la regresión de la etapa 3.

Resultado de esta entrega: cuatro proyectos y 11 vistas compilados, 58
comprobaciones de seguridad y 60 de catálogos correctas en
PostgreSQL temporal. Las pruebas de filtros usan contextos MVC de prueba;
las operaciones de negocio y datos sí usan PostgreSQL real.

Se intentó abrir `/Acceso/Ingresar` con Chromium contra un host ASP.NET temporal
en Mono 6.12 con el código y los ensamblados de la aplicación. El arranque
falló al resolver `System.Web.UI.ScriptResourceDefinition` en
`System.Web.Extensions`. No se retiraron componentes ni se modificó la
configuración de seguridad para evitar este límite de Mono. Por ello no se
afirma que haya pasado un recorrido HTTP completo de las pantallas.

La compilación y las pruebas con Mono no sustituyen la ejecución con .NET
Framework 4.8 e IIS Express. El recorrido de navegador completo se valida en
Windows antes de cerrar esta entrega y avanzar a productos.
