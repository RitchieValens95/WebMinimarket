# Etapa 4.2: productos

Esta entrega incorpora registro, listado, búsqueda, detalle, edición,
desactivación y reactivación de productos, además de la consulta de sus
movimientos de stock. Continúa sobre la etapa 4.1, conservando los cuatro
proyectos, .NET Framework 4.8, MVC 5, Bootstrap 5 y PostgreSQL 18.
Clientes y proveedores se desarrollarán después de revisar esta entrega.

## Recorrido de los archivos

```text
Navegador → ProductosController → ServicioProductos → ProductosRepositorio
                                                        ↓
                                                    PostgreSQL
Navegador ← vistas Razor ← entidades y modelos del formulario
```

- **Entidades — `Producto.cs`:** `SolicitudProducto` contiene los datos que
  se pueden guardar. `Producto` agrega la existencia actual y los nombres de
  categoría, marca y unidad para consultarlos. También define los resultados
  paginados, opciones de selección e historial. El contrato de escritura no
  contiene `StockActual`.
- **Datos — `ProductosRepositorio.cs`:** ejecuta SQL con parámetros y abre una
  transacción por operación. Consulta los catálogos existentes, sincroniza los
  códigos de barras y registra auditoría. Al editar bloquea la fila del producto
  y compara su versión. Las consultas de lectura usan una instantánea coherente
  para que el detalle y sus barras, o el stock y su historial, correspondan a
  la misma lectura.
- **Negocio — `ServicioProductos.cs`:** comprueba permisos, longitudes,
  referencias, rangos y precisión de decimales; evita identificadores repetidos
  y protege la unidad de medida cuando hay historial. No modifica existencias.
- **Web — `ProductosController.cs`:** recibe las peticiones y llama al servicio.
  `ViewModels/ProductoModelos.cs` admite coma o punto decimal sin depender del
  idioma configurado en Windows y convierte el formulario a `SolicitudProducto`.
  La lista explícita de campos admitidos excluye existencias y opciones.
- **Vistas — `Views/Productos/`:** `Index.cshtml` muestra búsqueda, filtros y
  páginas; `Formulario.cshtml` permite crear y editar; `Detalle.cshtml` muestra
  los datos y barras; `Movimientos.cshtml` consulta el historial real. Las vistas
  usan Bootstrap 5 y Razor codifica el texto que muestra.
- **Integración:** `Seguridad/Aplicacion.cs` crea el servicio con la conexión
  privada existente; `App_Start/RouteConfig.cs` registra `/Productos` antes de
  FriendlyUrls; `Views/Shared/_Layout.cshtml` agrega el enlace según el permiso.
  Los archivos nuevos están incluidos en los `.csproj` existentes y se guardan
  en UTF-8 con BOM para conservar las tildes.

## Reglas del producto

El código interno admite hasta 50 caracteres y la descripción hasta 200,
recortados y normalizados a Unicode NFC; ambos son obligatorios. La categoría
es obligatoria, la marca es opcional y la unidad se elige entre las unidades
reales de la base. Un producto activo requiere categoría y marca asignada
activas. Editar un producto inactivo conserva sus referencias actuales, aunque
se hayan desactivado; para reactivarlo debe seleccionar catálogos activos.

Los números se guardan como `decimal`, sin redondeo silencioso:

| Campo | Decimales máximos | Máximo admitido |
| --- | ---: | ---: |
| Precio de compra | 4 | 99999999999999,9999 |
| Precio de venta | 2 | 9999999999999999,99 |
| Stock mínimo | 3 | 999999999999999,999 |

Se aceptan valores mayores o iguales a cero. En el formulario puedes escribir
`0,250` o `0.250`, siempre **sin separadores de miles**. Para un producto que
no permite cantidades decimales, el stock mínimo debe ser entero. La unidad
KG no activa por sí sola esta opción: marca **Permite cantidades decimales**
si necesitas vender fracciones de kilogramo.

Puedes registrar hasta 50 códigos de barras, uno por línea y hasta 100
caracteres cada uno. Se tratan como texto: `001234` conserva sus ceros.
El código interno es único ignorando mayúsculas y espacios iniciales/finales;
las barras se comparan de forma literal. Los identificadores de productos
inactivos siguen reservados mientras estén asociados a ellos. Para quitar una
barra, elimínala del formulario y guarda; las barras conservadas mantienen su id.

La aplicación también rechaza que un código interno coincida con la barra de
otro producto, o viceversa. Esta regla compartida se comprueba bajo un bloqueo
transaccional de las escrituras del módulo; los índices existentes de
PostgreSQL garantizan la unicidad de códigos y barras por separado.

La búsqueda admite una parte de la descripción, el código interno completo
sin distinguir mayúsculas o cualquiera de sus barras completas. Las coincidencias
exactas de códigos se muestran primero. Con el foco en Buscar, un lector que
escribe el código y envía Enter ejecuta la búsqueda normal. También puedes
filtrar categoría, marca, estado y stock bajo/agotado. Se muestran 20 productos
por página y los filtros se conservan al cambiar de página. `%` y `_` actúan
como comodines en la búsqueda de descripción.

La versión `xmin` detecta formularios antiguos, incluso si solo cambiaron las
barras o si otra operación modificó el stock. Ante un conflicto vuelve al
listado y abre Editar nuevamente. Las escrituras confirmadas registran actor,
acción e identificador en auditoría dentro de la misma transacción.

## Existencias e historial

Un producto nuevo comienza con existencia cero. El formulario no permite
registrar un saldo inicial ni modificar directamente la existencia actual.
Compras, ventas y ajustes deberán registrar el movimiento que respalda cada
cambio cuando se implementen esos módulos. No se crean movimientos ficticios.

Desactivar conserva el producto, sus barras, existencias e historial; se puede
reactivar desde Editar. Si el producto tiene existencias, movimientos o detalles
de compras/ventas, no se permite cambiar su unidad ni su configuración decimal.
La regla se conserva aunque el saldo vuelva a cero: las cantidades antiguas
siguen expresadas en la unidad original. Antes de tener historial y con saldo
cero sí se pueden corregir esas opciones.

Movimientos muestra fecha en horario de Paraguay, tipo, cantidad, saldos,
costo, usuario y observación, con 20 filas por página. La existencia y los
movimientos se consultan desde una misma instantánea de PostgreSQL. No se
edita ni elimina el historial desde esta pantalla.

## Permisos

- `PRODUCTOS_VER`: listado, detalle y opciones para los filtros.
- `PRODUCTOS_VER` y `PRODUCTOS_EDITAR`: crear, editar, desactivar y reactivar.
- `PRODUCTOS_VER` y `STOCK_VER`: consultar movimientos.

Administrador y Supervisor tienen inicialmente todos esos permisos. Cajero
puede consultar productos, pero no modificarlos ni consultar movimientos.
Se comprueban los permisos en MVC y nuevamente en el servicio; ocultar botones
no es la única protección. Guardar y Desactivar usan POST y token
antifalsificación. Las peticiones requieren la configuración HTTPS existente.

## Incorporar en tu notebook

Esta entrega utiliza las tablas e índices existentes: **no hay una nueva
migración SQL ni paquetes NuGet adicionales**. Debe estar aplicada y revisada
la etapa 4.1, incluida la versión 006. No recrees la base ni repitas migraciones
anteriores para actualizar los productos.

Cuando el código de esta entrega esté disponible:

1. Detén la aplicación en Visual Studio. Ejecuta `git status --short` en
   `D:\Sistemas\SistemaVentas` y conserva tus cambios locales si aparecen.
   Sigue los comandos de actualización de la rama indicados en el chat.
2. Abre la solución de **esa misma carpeta**, restaura NuGet si Visual Studio
   lo solicita y recompila. Conserva `AppSettings.Local.config` fuera de Git.
3. Inicia por HTTPS e ingresa como administrador. Abre **Productos** desde
   el menú. No necesitas crear otra cuenta ni modificar la conexión.
4. Puedes ejecutar `database/007_comprobar_catalogos.sql` y
   `database/003_comprobar_estructura.sql` si quieres confirmar que la base
   sigue íntegra; ambos son verificadores, no una reinstalación.

## Recorrido de revisión en Windows

1. Crea una categoría y una marca activas, si no tienes una apropiada.
2. Registra un producto con código `CAFE-KG`, descripción `Café por kilogramo`,
   unidad KG, cantidades decimales activadas, precio de compra `10,1234`,
   venta `15,50`, mínimo `0,250` y dos barras en líneas separadas:
   `0012345678901` y `0012345678902` (usa otros valores si ya existen).
3. Comprueba los valores en el detalle. La existencia inicial debe ser cero
   y las barras deben conservar los ceros iniciales. Busca por parte de la
   descripción, código completo y cada barra. Prueba el lector con Enter.
4. Intenta repetir el código en otro producto o usar una de sus barras: debe
   rechazarlo. Prueba un precio negativo o venta con tres decimales; no debe
   redondear ni guardar. En un producto sin cantidades decimales, prueba un
   mínimo `0,250`: debe rechazarlo.
5. Edita un precio y reemplaza una barra. Comprueba que los demás datos y la
   existencia se conserven. La barra quitada ya no debe encontrar ese producto.
6. Abre el mismo producto para editar en dos pestañas. Guarda en la primera;
   guardar desde la segunda debe rechazar la versión antigua.
7. Desactiva y filtra Inactivos. Reactiva desde Editar. El producto debe
   conservar sus datos y su identificador. Prueba filtros y paginación si
   tienes más de 20 productos.
8. Abre Movimientos. Para un producto nuevo debe estar vacío. Si tienes
   movimientos de demostración válidos, deben aparecer sin modificarse, y
   cambiar la unidad del producto debe rechazarse incluso con saldo cero.
9. Con Cajero, consulta el listado y el detalle. No deben aparecer botones de
   edición ni movimientos. Las URL `/Productos/Crear` y
   `/Productos/Movimientos/{id}` deben dar 403 sin los permisos correspondientes.
10. Comprueba tildes, formularios y tablas en una ventana estrecha, además de
    Inicio, Categorías, Marcas, Usuarios, Roles y Cerrar sesión.

## Pruebas y límites de esta entrega

`tests/productos/ProductosPruebas.cs` ejecuta el servicio y repositorio contra
PostgreSQL 18 real. Comprueba precisión decimal, búsqueda, referencias,
identificadores, stock de solo lectura, conservación de historial, permisos,
auditoría y concurrencia. Los filtros MVC se prueban con contextos de prueba;
no se presenta esto como un recorrido HTTP completo.

```bash
python3 tests/seguridad/ejecutar.py --productos \
  --dotnet /ruta/al/dotnet \
  --referencias /ruta/al/paquete/net48/build/.NETFramework/v4.8
```

Se necesitan los paquetes restaurados en `packages/`, las referencias oficiales
net48 y Docker con `postgres:18` y `mono:6.12`. El ejecutor crea y elimina una
base temporal con credenciales aleatorias; las etapas anteriores solo se
instalan como preparación de esa base descartable. No conecta a tu base de
Windows. `--catalogos` prueba la etapa 4.1; sin ninguna de esas dos opciones,
prueba seguridad. Los cuatro proyectos y las 15 vistas Razor se compilan contra
.NET Framework 4.8 en cada ejecución.

Resultado de esta entrega: **75 comprobaciones de productos**, **60 de
catálogos** y **58 de seguridad** correctas, junto con la compilación de los
cuatro proyectos y las 15 vistas. También se ejecutaron los verificadores
existentes del esquema y la comprobación transaccional de la migración 006.

Se intentó abrir la aplicación con Chromium y un host ASP.NET temporal en
Mono 6.12. El arranque encontró la incompatibilidad existente al resolver
`System.Web.UI.ScriptResourceDefinition` de `System.Web.Extensions`. No se
retiraron componentes del proyecto para evitarla. Por eso el recorrido de las
pantallas queda pendiente en Windows con Visual Studio e IIS Express antes de
cerrar esta etapa.
