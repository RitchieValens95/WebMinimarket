# Etapa 4.3: clientes

Esta entrega incorpora registro, listado, búsqueda, detalle, edición,
desactivación, reactivación e historial de compras de clientes. Conserva los
cuatro proyectos, .NET Framework 4.8, ASP.NET MVC 5, Bootstrap 5 y PostgreSQL 18.
Continúa desde la etapa 4.2 validada. Proveedores corresponde a la siguiente
entrega, después de revisar y confirmar esta etapa.

## Cómo se relacionan los archivos

```text
Formulario → ClientesController → ServicioClientes → ClientesRepositorio
                                                          ↓
                                                      PostgreSQL
Navegador ← vistas Razor ← entidades del cliente y resultados de consulta
```

- **Entidades — `Cliente.cs`:** `SolicitudCliente` contiene los campos que se
  admiten al guardar; `Cliente` agrega el indicador de cliente genérico leído
  de la base. También representa páginas de clientes e historial de compras.
- **Datos — `ClientesRepositorio.cs`:** ejecuta consultas con parámetros, usa
  transacciones y registra auditoría. Las lecturas usan una misma instantánea
  de PostgreSQL para que el total y las filas correspondan al mismo momento.
  La edición bloquea la fila y comprueba la versión `xmin`.
- **Negocio — `ServicioClientes.cs`:** verifica permisos, valida documentos y
  contactos, protege al cliente genérico y limita el historial según el actor.
- **Web — `ClientesController.cs`:** recibe las peticiones y usa el servicio.
  Registrar y Guardar son acciones distintas porque sus permisos son distintos.
  `ViewModels/ClienteModelos.cs` describe el formulario y sus validaciones.
- **Vistas — `Views/Clientes/`:** `Index.cshtml` muestra búsqueda, estado y
  páginas; `Formulario.cshtml` crea o edita; `Detalle.cshtml` consulta los datos;
  `Historial.cshtml` consulta compras confirmadas. Razor codifica el texto que
  muestra. Los archivos nuevos usan UTF-8 con BOM para conservar las tildes.
- **Integración:** `Seguridad/Aplicacion.cs` crea el servicio con la conexión
  privada existente. `App_Start/RouteConfig.cs` registra `/Clientes` antes de
  FriendlyUrls. `Views/Shared/_Layout.cshtml` muestra el enlace con permiso.
  Los archivos nuevos están incluidos en los `.csproj` existentes.

No se cambian tablas, paquetes NuGet, versiones de framework ni configuración
privada. **Esta entrega no necesita una migración SQL nueva.**

## Datos y reglas

El nombre es obligatorio y admite hasta 150 caracteres. Se recortan espacios
al principio/final, se normaliza Unicode NFC y se rechazan caracteres de control
y saltos de línea. Los demás campos son opcionales:

| Campo | Límite | Regla |
| --- | ---: | --- |
| Tipo de documento | 20 | CI, RUC, PASAPORTE u OTRO |
| Documento o RUC | 40 | Debe acompañar al tipo, o dejar ambos vacíos |
| Teléfono | 40 | Se conserva su formato, incluido el prefijo internacional |
| Dirección | 250 | Una línea de texto |
| Correo electrónico | 254 | Una dirección de correo, sin nombre adicional |

No se convierten documentos a números: se conservan ceros iniciales y guiones.
El tipo se guarda en mayúsculas. El par tipo+número no puede repetirse ignorando
mayúsculas y espacios iniciales/finales, incluso si el cliente está inactivo.
Un mismo número con tipos distintos es otro documento. Puede haber nombres
iguales y varios clientes sin documento. Los campos opcionales vacíos se guardan
como NULL, sin números ni correos ficticios.

Buscar admite una parte del nombre, documento o RUC; `%` y `_` conservan su
significado de comodines de PostgreSQL. Todas las entradas usan parámetros SQL.
El listado tiene filtros Todos/Activos/Inactivos y páginas de 20 clientes. El
cliente genérico se muestra primero; luego se ordena por nombre e ID.

**Consumidor final** es el cliente genérico existente para operaciones sin
identificación del comprador. Desde la aplicación se puede consultar su ficha
e historial, pero no editarlo ni desactivarlo. El indicador `es_generico` no
forma parte del contrato de escritura ni del formulario recibido.

Desactivar conserva el ID, contactos, documento e historial. Reactivar consiste
en abrir Editar, marcar Activo y guardar. El documento queda reservado mientras
esté asociado al cliente inactivo. No existe una acción de eliminación física.

Al editar se compara `xmin`, la versión de la fila: una ventana antigua no puede
sobrescribir el cambio de otra. Ante un conflicto vuelve al listado y abre Editar
nuevamente. Crear, editar y desactivar registran actor, acción e ID en auditoría
en la misma transacción; una operación rechazada no registra un éxito parcial.

## Permisos e historial

| Operación | Permisos necesarios |
| --- | --- |
| Listado y detalle | CLIENTES_VER |
| Crear | CLIENTES_VER + CLIENTES_REGISTRAR |
| Editar, desactivar o reactivar | CLIENTES_VER + CLIENTES_EDITAR |
| Historial completo | CLIENTES_VER + VENTAS_VER_TODAS |
| Historial propio | CLIENTES_VER + VENTAS_VER_PROPIAS |

Administrador y Supervisor tienen inicialmente los permisos de administración.
Cajero puede consultar y registrar clientes activos; no puede editar ni
desactivar clientes existentes, ni registrar un inactivo sin permiso de edición.
Los roles personalizados se comprueban de nuevo en cada operación. Las acciones
POST usan token antifalsificación y la configuración HTTPS existente.

El historial muestra las ventas **CONFIRMADAS** asociadas al cliente: número,
fecha en horario de Paraguay, vendedor y total. Los borradores no cuentan como
compras. Con permiso de ventas propias, el cajero solo ve sus documentos: tanto
las filas como el contador y el importe acumulado aplican ese filtro. Un permiso
de consulta de clientes por sí solo no permite ver ventas. Hay 20 filas por
página, con orden estable por fecha e ID.

Si todavía no registraste ventas confirmadas, el historial estará vacío. La
pantalla no crea ventas ni movimientos de stock para mostrar resultados. Tampoco
cambia documentos anteriores al editar o desactivar un cliente. La compra se
muestra desde la perspectiva del cliente; la fuente es la tabla de ventas del
comercio, no las compras a proveedores.

## Incorporar en tu notebook cuando la rama esté disponible

1. Detén IIS Express y comprueba `git status --short` en
   `D:\Sistemas\SistemaVentas`. Si hay cambios locales, consérvalos antes de
   actualizar; no fuerces la combinación ni sobrescribas archivos.
2. Incorpora la rama de esta entrega siguiendo los comandos indicados en el
   chat. No necesitas ejecutar otro script SQL ni recrear la base. La etapa 4.2
   y sus actualizaciones anteriores deben seguir aplicadas y validadas.
3. Abre la solución de **esa misma carpeta** en Visual Studio, restaura NuGet
   si lo solicita y recompila. Mantén `AppSettings.Local.config` fuera de Git.
4. Inicia por HTTPS e ingresa con tu cuenta existente. Abre **Clientes** desde
   el menú. No se crean cuentas ni contraseñas predeterminadas.

## Recorrido de comprobación en Windows

1. Confirma que aparece Consumidor final con la indicación Genérico. Abre su
   detalle: no debe ofrecer edición ni desactivación.
2. Registra un cliente de prueba con nombre, RUC, teléfono, dirección y correo.
   Usa un documento que no esté registrado. Comprueba los datos y las tildes
   en el detalle; busca por una parte del nombre y del RUC.
3. Intenta repetir el tipo y número en otro cliente: debe rechazarlo. Prueba
   dejar solo el tipo o solo el número; debe pedir completar el par. Registra
   otro cliente sin documento, dejando ambos campos vacíos.
4. Edita teléfono o dirección y guarda. Abre la misma ficha para editar en dos
   pestañas: guarda en la primera e intenta guardar en la segunda. Debe rechazar
   la versión antigua sin sobrescribir datos.
5. Desactiva el cliente de prueba, filtra Inactivos y vuelve a editarlo. Marca
   Activo y guarda. Debe conservar su ID y su documento.
6. Prueba búsqueda, filtros y páginas si tienes más de 20 clientes. Comprueba
   que el formulario y la tabla se puedan usar en una ventana estrecha.
7. Abre Historial de compras. Si aún no hay ventas confirmadas, debe estar vacío.
   Si hay datos anteriores, comprueba que sean ventas confirmadas del cliente.
8. Con una cuenta Cajero, consulta y registra un cliente activo. Los botones
   de edición y desactivación no deben aparecer. La URL `/Clientes/Editar/{id}`
   debe responder 403. En Historial, el aviso debe indicar que muestra solo
   ventas propias; no debe revelar importes de otros vendedores.
9. Comprueba que Inicio, Productos, Categorías, Marcas, Usuarios, Roles y Cerrar
   sesión sigan funcionando con los permisos correspondientes.

## Validación automatizada y límites

`tests/clientes/ClientesPruebas.cs` usa PostgreSQL 18 real en una base temporal
para comprobar registro, contacto, documentos, opcionales, cliente genérico,
permisos, auditoría, filtros, páginas, reactivación y conflictos concurrentes.
También prepara ventas completas con detalle, pago, movimientos coherentes y
auditoría para comprobar que el historial y sus importes respetan el vendedor.
Esa preparación es exclusiva de la prueba; no implementa el POS ni se aplica
a la base del usuario. Los filtros MVC usan contextos de prueba y no se presentan
como solicitudes HTTP completas.

```bash
python3 tests/seguridad/ejecutar.py --clientes \
  --dotnet /ruta/al/dotnet \
  --referencias /ruta/al/paquete/net48/build/.NETFramework/v4.8
```

El ejecutor necesita los paquetes de `packages/`, las referencias oficiales
net48 y Docker con `postgres:18` y `mono:6.12`. Crea y elimina una base temporal
con credenciales aleatorias. Las migraciones anteriores solo se instalan allí
como preparación. No conecta al PostgreSQL de tu notebook.

La compilación y las pruebas con Mono no sustituyen el recorrido en Windows
con .NET Framework 4.8 e IIS Express. La revisión de las pantallas en ese entorno
se completa antes de avanzar a proveedores.

Resultado de esta entrega: **60 comprobaciones de clientes**, **125 de
productos** y **58 de seguridad** correctas; compilación de los cuatro
proyectos y las **19 vistas Razor** contra net48. Los scripts SQL anteriores
se conservaron íntegros y los verificadores del esquema pasaron en las bases
temporales correspondientes.

Se intentó abrir la aplicación real con Chromium contra un host ASP.NET
temporal en Mono 6.12. El arranque encontró la incompatibilidad existente
con `System.Web.UI.ScriptResourceDefinition` en `System.Web.Extensions`.
Por eso no se afirma que haya pasado el recorrido HTTP completo de clientes;
la comprobación de las pantallas queda pendiente en IIS Express.
