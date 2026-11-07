# Etapa 2 original: estructura de PostgreSQL

La etapa 2 ya fue validada en Windows. Si continúas con acceso y usuarios,
consulta [etapa 3](../docs/etapa-3-seguridad.md): solo requiere la nueva migración
004 sobre la base existente, no repetir las migraciones iniciales.

Estos scripts implementan la base del sistema del prompt original. Se mantienen
los cuatro proyectos de Visual Studio, .NET Framework 4.8 y MVC 5.
Se crean las tablas y restricciones que utilizarán los módulos posteriores;
todavía no hay pantallas de usuarios, productos, ventas o caja.

## Archivos y orden

| Archivo | Función |
| --- | --- |
| `001_estructura.sql` | 25 tablas, 41 índices explícitos además de PK/UNIQUE, secuencia de ventas y 18 triggers de integridad. |
| `002_datos_iniciales.sql` | Tres roles, 26 permisos y su asignación, unidades, métodos de pago, cliente genérico, categoría, caja y configuración del comercio. |
| `003_comprobar_estructura.sql` | Verificación de solo lectura: versiones, tablas, restricciones, índices, triggers, catálogos y saldo de stock. |
| `demo/001_productos.sql` | Opcional: tres productos DEMO, sin existencias ni cuentas de acceso. |
| `tests/001_integridad.sql` | Pruebas SQL transaccionales para una base descartable. |
| `tests/ejecutar.py` | Batería automática con Docker y Python 3; crea y elimina su propio PostgreSQL 18. |

## Qué tablas se crean

- Seguridad: usuarios, roles, permisos, usuarios_roles y roles_permisos.
- Personas/catálogos: clientes, proveedores, categorias, marcas,
  unidades_medida, metodos_pago y configuracion_comercio.
- Productos: productos y productos_codigos_barras.
- Ventas: ventas, ventas_detalles y pagos.
- Compras: compras y compras_detalles.
- Inventario: movimientos_stock.
- Caja: cajas, cajas_sesiones y cajas_movimientos.
- Control: auditoria y esquema_versiones.

El rol de PostgreSQL `sistemaventas_app` sigue siendo la cuenta técnica de
conexión. Los roles Administrador/Supervisor/Cajero se guardan en tablas de la
aplicación. No se crea un administrador con contraseña predeterminada: la cuenta
inicial de acceso se configurará de forma segura en la etapa 3.

## Obtener los archivos en Windows

La rama de entrega es `codex/etapa2-postgresql`. Si tu carpeta de D: conserva Git,
cierra Visual Studio y comprueba que no tengas cambios pendientes:

```powershell
git -C "D:\Sistemas\SistemaVentas" status --short
```

Si está limpio, ejecuta por separado:

```powershell
git -C "D:\Sistemas\SistemaVentas" fetch origin
git -C "D:\Sistemas\SistemaVentas" merge --ff-only origin/codex/etapa2-postgresql
```

Si un comando falla, detente. No uses reset ni fuerces una fusión.
Si trabajas con el ZIP de GitHub, puedes copiar únicamente la carpeta `database`
a `D:\Sistemas\SistemaVentas\database`; esta entrega no necesita reemplazar
proyectos, vistas, binarios ni el archivo privado de conexión.

## Aplicar en pgAdmin 4

1. Conecta al servidor local y selecciona la base **sistemaventas**, propiedad de
   **sistemaventas_app**. Abre Query Tool desde esa base.
2. Antes de cambiar nada, ejecuta estas consultas de inspección:

```sql
SELECT current_database() AS base, current_user AS usuario, version();
SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename;
```

La base debe ser la de la aplicación. En una instalación nueva la segunda
consulta estará vacía. Si ya hay tablas del sistema, conserva el resultado para
revisarlo; el script detiene los conflictos que no pertenecen a sus migraciones.
No borres tablas para conseguir que pase.

3. Si ya tienes datos propios, realiza un respaldo con la opción **Backup** de
   pgAdmin antes de aplicar cambios de estructura.
4. Abre `001_estructura.sql` con el botón de abrir archivo de Query Tool. Ejecuta
   **todo el archivo** (Ctrl+A y F5). Debe finalizar con COMMIT y un aviso de éxito.
5. Solo si terminó sin errores, abre y ejecuta completo `002_datos_iniciales.sql`.
6. Ejecuta completo `003_comprobar_estructura.sql`. El resultado esperado es:

| estado | tablas_sistema | roles | permisos | metodos_pago | zona_horaria |
| --- | --- | --- | --- | --- | --- |
| OK | 25 | 3 | 26 | 4 | America/Asuncion |

7. Actualiza el nodo **Schemas → public → Tables** en pgAdmin para ver las tablas.
8. Comprueba otra vez la aplicación con `/Inicio/Conexion` y el inicio MVC.
   Los scripts no cambian tu conexión local.

Si un script falla, detente y comparte el error sin contraseñas. Si Query Tool
indica que la transacción está abortada, ejecuta `ROLLBACK;` para cerrarla antes
de volver a probar. Un error no autoriza a eliminar datos ni editar la tabla de
versiones para saltarse la comprobación.

## Repetición, versiones y permisos

- 001 y 002 son transacciones completas. Un error revierte sus cambios.
- Las ejecuciones se coordinan con un bloqueo transaccional para impedir
  instalaciones simultáneas incompatibles.
- `esquema_versiones` guarda la versión y el SHA-256 del bloque de cada migración.
  El cálculo normaliza CRLF/LF, por lo que un checkout de Windows no cambia la firma.
  Los comentarios externos al bloque no forman parte del checksum.
- Volver a ejecutar el mismo archivo produce un aviso de “ya aplicada” y no
  cambia los datos. No repone permisos retirados ni sobrescribe catálogos editados.
- Para cambiar una versión ya aplicada hay que crear una nueva migración, no
  editar su cuerpo, borrar su registro o sustituir su checksum.
- La extensión estándar `pg_trgm` se instala en `public`. Un propietario de la
  base sin superusuario puede instalarla en PostgreSQL 18. Si falta en la
  instalación del servidor o existe en otro esquema, el script se detiene.
- El verificador detecta cambios básicos de estructura; no es una herramienta
  de reparación ni reemplaza un respaldo.

## Contrato de las operaciones futuras

Los triggers comprueban integridad, no implementan por sí solos el POS ni conceden
permisos de aplicación. Los servicios de Negocio/Datos deberán seguir este orden:

1. Iniciar una transacción y crear la cabecera en BORRADOR.
2. Registrar detalles y actualizar el stock de manera condicionada a existencia
   suficiente, bloqueando productos en orden estable.
3. Insertar el movimiento de stock correspondiente, después del cambio de stock.
   Su saldo anterior debe ser el último saldo y su saldo posterior el stock actual.
4. Registrar pagos y, solo para efectivo, su ingreso neto de vuelto en caja.
5. Insertar auditoría con entidad `ventas`/`compras`, id de cabecera y acción
   `VENTA_CONFIRMADA`/`COMPRA_CONFIRMADA`, usando el usuario responsable.
6. Cambiar la cabecera a CONFIRMADA y confirmar la transacción.

Al COMMIT se verifican detalle, totales, pagos, movimientos y auditoría. Un pago
o movimiento comercial no puede quedar guardado sin confirmar su documento.
Los registros confirmados y los historiales de stock/caja/auditoría son inmutables.
Los borradores pueden prepararse sin efectos de stock ni pagos persistidos.

Las cantidades usan tres decimales; los importes cobrados dos y los costos cuatro.
La aplicación validará la escala antes de enviar valores: PostgreSQL redondea
entradas que exceden la escala de una columna NUMERIC. Se rechazan importes NaN,
cantidades inválidas y stock negativo. Los correlativos pueden dejar huecos ante
una reversión, pero no duplicarse. La solicitud guarda una clave UUID única y un
hash para que el servicio pueda comprobar que un reintento tiene el mismo contenido.

Las fechas se guardan como instantes TIMESTAMPTZ y el comercio usa
America/Asuncion. La configuración horaria de estos scripts es local a su
transacción; la presentación y los reportes usarán la zona del comercio en sus
etapas de implementación.

## Productos de prueba opcionales

Después de los tres scripts principales puedes ejecutar `demo/001_productos.sql`.
Crea DEMO-UN, DEMO-KG y DEMO-FRAC para unidad, peso y unidad fraccionable. Sus códigos
de barras son textos de demostración y su stock inicial es cero. Se podrá cargar
existencia mediante movimientos con usuario responsable cuando ese módulo exista.
Repetir el archivo no sobrescribe precios, stock ni códigos previamente cargados.

## Pruebas automáticas para desarrollo

Con Docker local disponible, desde la raíz del repositorio:

```bash
python3 database/tests/ejecutar.py
```

El ejecutor usa la imagen oficial `postgres:18`, un contenedor con nombre aleatorio
y un rol sin superusuario. No publica puertos ni conecta a tu base de Windows.
El contenedor y sus bases temporales se eliminan al terminar, incluso si falla una
prueba. Los archivos SQL funcionan en pgAdmin sin instalar Python ni Docker.

No ejecutes `tests/001_integridad.sql` en una base con operaciones reales:
aunque revierte sus filas, las secuencias avanzan. La ruta de pruebas se entrega
para desarrolladores y se ejecutó sobre una base descartable.

## Evidencia de validación de esta entrega

- PostgreSQL 18.6: 44 comprobaciones SQL y 32 del ejecutor, todas correctas.
- Instalación y repetición con un rol sin superusuario; CRLF de Windows y LF
  producen los mismos checksums. Los archivos SQL se guardan en UTF-8 con firma
  para facilitar su apertura con acentos en Windows.
- Dos aperturas concurrentes y dos solicitudes con la misma clave: solo una
  operación se registra en cada caso. El descuento condicionado de stock no
  consume dos veces la última unidad.
- Conflictos, errores y migraciones modificadas se rechazan sin sobrescribir
  datos previos. El verificador detecta índices faltantes y triggers desactivados.
- Compilación directa del C# de los cuatro proyectos con Roslyn y referencias
  oficiales de .NET Framework 4.8: sin errores. No cambiaron los proyectos C#.
- Ejecutable compilado para .NET Framework y ejecutado con Mono 6.12/Npgsql 4.1.14:
  conexión SCRAM con el esquema final, lectura de 25 tablas y sus versiones,
  parámetros decimales 0.250/123.4567 y lectura de TIMESTAMPTZ correctos.

Las pruebas de contenedores/Mono no sustituyen la aplicación en tu PostgreSQL
de Windows ni la compilación y navegación final con Visual Studio/IIS Express.

## Actualización de la etapa 4.1

Para una base con la etapa 3 ya validada, aplica únicamente
`006_catalogos.sql` y verifica con `007_comprobar_catalogos.sql`. No repitas
las migraciones anteriores para esta actualización. Las reglas de productos
activos, archivos relacionados y pasos para Windows se explican en
[la guía de categorías y marcas](../docs/etapa-4-1-catalogos.md).

## Corrección de productos: IVA y EAN-13

Sobre la base con 006 validada, aplica `008_producto_iva_ean13.sql` y verifica
con `009_comprobar_producto_iva_ean13.sql`. 008 no borra barras ni asigna IVA
a los productos anteriores: detiene la actualización si hay más de una barra
por producto y deja el impuesto anterior pendiente. Consulta el procedimiento
previo de selección de barra y los pasos de actualización en
[la guía de productos](../docs/etapa-4-2-productos.md).

## Productos de balanza

Sobre 008 ya validada, ejecuta `010_productos_balanza.sql` y después
`011_comprobar_productos_balanza.sql`. Los indicadores nuevos comienzan en
falso y los datos anteriores se conservan. El prefijo fijo guardado es de
siete dígitos; el peso/cantidad de cada etiqueta no se guarda como parte del
código del producto. Los pasos están en la guía de productos.
