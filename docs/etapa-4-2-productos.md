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
  transacción por operación. Consulta los catálogos existentes, guarda la
  barra única y registra auditoría. Al editar bloquea la fila del producto
  y compara su versión. Las consultas de lectura usan una instantánea coherente
  para que el detalle y su barra, o el stock y su historial, correspondan a
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

Cada producto tiene **un solo código de barras** de hasta 100 caracteres,
tratado como texto para conservar los ceros iniciales. Si dispone de código,
escríbelo en el campo único. Si no lo tiene, marca **El producto no tiene código
de barras** y se mostrará su EAN-13 interno. Esta regla sustituye la variante
inicial que admitía varias barras por producto.

Al pulsar Nuevo producto se reserva un ID usando la secuencia de PostgreSQL,
sin insertar productos vacíos. La reserva está protegida contra alteraciones y
vinculada al usuario; el formulario tiene una vigencia de 8 horas. Dos usuarios
o pestañas reciben números distintos. Cancelar o fallar una operación puede
consumir un número: son identificadores únicos, no una numeración sin saltos.

El EAN-13 usa `28` más el ID completado con ceros hasta diez posiciones;
a los doce dígitos resultantes se agrega el control de EAN-13:

| ID del producto | Base de 12 dígitos | Control | EAN-13 |
| ---: | --- | ---: | --- |
| 26 | 280000000026 | 4 | 2800000000264 |
| 36 | 280000000036 | 3 | 2800000000363 |

El servidor vuelve a calcular la barra al guardar. La casilla cambia el campo
a solo lectura para mostrar ese valor; no se confía en un código automático
modificado desde el navegador. La base comprueba al confirmar la transacción
que una barra marcada como generada corresponda al ID real del producto.
Los códigos manuales conservan su formato original; no se obliga a que todos
los fabricantes utilicen EAN-13. La generación interna admite IDs de 1 a
9999999999 y no trunca identificadores más largos.

El código interno sigue siendo un campo separado y editable; en un formulario
nuevo se propone el número reservado. No es el dato usado para calcular el
EAN: aunque edites el código interno, la barra automática conserva el ID real.
Los códigos de productos inactivos siguen reservados mientras estén asociados.
La aplicación rechaza conflictos entre códigos internos y barras de otros
productos; los índices de PostgreSQL garantizan la unicidad de cada tipo y
una sola barra por producto. Al sustituir una barra se conserva su fila e ID.

### IVA del producto

Selecciona **Exento (0 %), 5 % o 10 %**. Los precios de compra y venta ingresados
**ya incluyen IVA**, según lo confirmado por el usuario. Elegir o cambiar el
porcentaje no vuelve a sumar el impuesto ni altera los importes guardados.
El porcentaje se muestra en listado y detalle y debe elegirse explícitamente
al crear o editar. La liquidación y copia de la tasa a cada detalle de venta o
compra se implementarán con esos módulos, conservando el impuesto histórico.

Los productos anteriores se muestran con **IVA pendiente** hasta que el usuario
seleccione su tasa correcta; no se adivina una tasa ni se los clasifica como
exentos automáticamente. Un campo pendiente no equivale a 0 %.

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

La corrección de IVA y barra única requiere **008_producto_iva_ean13.sql**
sobre la base existente con la etapa 4.1 (006) validada. No agrega paquetes
NuGet ni reemplaza proyectos. No recrees la base ni repitas 001, 002, 004 o 006.

Antes de actualizar, esta consulta de solo lectura identifica productos con
más de una barra:

```sql
SELECT p.id, p.codigo, p.descripcion, array_agg(b.codigo ORDER BY b.id) AS barras
FROM public.productos p JOIN public.productos_codigos_barras b ON b.producto_id=p.id
GROUP BY p.id,p.codigo,p.descripcion HAVING count(*)>1 ORDER BY p.id;
```

Si hay resultados, elige cuál conservar en la pantalla anterior antes de aplicar
008. La migración se detiene mostrando los IDs y no elimina barras por ti.
La información de precios, stock, documentos e historial se conserva.

Cuando el código de esta entrega esté disponible:

1. Detén la aplicación en Visual Studio. Ejecuta `git status --short` en
   `D:\Sistemas\SistemaVentas` y conserva tus cambios locales si aparecen.
   Sigue los comandos de actualización de la rama indicados en el chat.
2. En pgAdmin abre el Query Tool de tu base del sistema y ejecuta
   `database/008_producto_iva_ean13.sql`. Debe terminar en COMMIT y avisar que
   se aplicó 008. Luego ejecuta `database/009_comprobar_producto_iva_ean13.sql`.
   Debe verificar las restricciones y mostrar cuántos productos tienen IVA
   pendiente. Repetir la migración idéntica no duplica datos; un checksum
   modificado se rechaza. Si hay varias barras o un bloqueo, la transacción
   completa se revierte: revisa el mensaje antes de repetir.
3. Abre la solución de **esa misma carpeta**, restaura NuGet si Visual Studio
   lo solicita y recompila. Conserva `AppSettings.Local.config` fuera de Git.
4. Inicia por HTTPS e ingresa como administrador. Abre **Productos** desde
   el menú. No necesitas crear otra cuenta ni modificar la conexión.
5. Puedes ejecutar `database/007_comprobar_catalogos.sql` y
   `database/003_comprobar_estructura.sql` si quieres confirmar que la base
   sigue íntegra; ambos son verificadores, no una reinstalación.

## Recorrido de revisión en Windows

1. Crea una categoría y una marca activas, si no tienes una apropiada.
2. Registra un producto con código `CAFE-KG`, descripción `Café por kilogramo`,
   unidad KG, cantidades decimales activadas, precio de compra `10,1234`,
   venta `15,50`, mínimo `0,250`, IVA 10 % y una barra de fabricante
   (usa un valor que no esté registrado).
3. Comprueba los valores en el detalle. La existencia inicial debe ser cero
   y la barra debe conservar los ceros iniciales. Busca por parte de la
   descripción, código completo y barra. Prueba el lector con Enter.
4. Intenta repetir el código en otro producto o usar su barra: debe
   rechazarlo. Prueba un precio negativo o venta con tres decimales; no debe
   redondear ni guardar. En un producto sin cantidades decimales, prueba un
   mínimo `0,250`: debe rechazarlo.
5. Edita un precio y reemplaza la barra. Comprueba que los demás datos y la
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

También comprueba la corrección:

1. Pulsa Nuevo producto, anota el número y marca la casilla de producto sin
   barra. Debe mostrar 13 dígitos, empezar con 28 y conservarse al guardar.
2. Abre dos formularios nuevos: sus números y barras deben ser distintos.
   Puedes guardarlos en orden inverso. Cancelar uno no crea un producto vacío.
3. Prueba marcar y desmarcar la casilla: la barra manual se recupera y el campo
   automático queda de solo lectura. No hay área de texto para varias barras.
4. Selecciona Exento, 5 % o 10 %; guarda y revisa listado/detalle. Cambiar la
   tasa debe conservar los precios. Un producto anterior obliga a elegir IVA
   antes de guardar una edición.

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

La corrección pasó **99 comprobaciones de productos**, **60 de catálogos** y
**58 de seguridad**, incluyendo comparación
entre SQL y C# para 1000 IDs, reservas simultáneas, protección de reservas,
IVA y restricciones reales de la base. Compilan los cuatro proyectos y las
15 vistas. Se comprueba que 008 se repite sin duplicar, rechaza un checksum
distinto y revierte por completo si existen varias barras por producto.

`tests/productos/probar_formulario.py` verifica con Chromium/Playwright el
script real de la casilla (campo manual/automático, solo lectura y recuperación
del valor manual) en un DOM aislado. Esta prueba del componente no se presenta
como un recorrido HTTP completo de ASP.NET.

Se intentó abrir la aplicación con Chromium y un host ASP.NET temporal en
Mono 6.12. El arranque encontró la incompatibilidad existente al resolver
`System.Web.UI.ScriptResourceDefinition` de `System.Web.Extensions`. No se
retiraron componentes del proyecto para evitarla. Por eso el recorrido de las
pantallas queda pendiente en Windows con Visual Studio e IIS Express antes de
cerrar esta etapa.
