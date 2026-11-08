# Etapa 4.4: proveedores

Esta entrega incorpora registro, listado, búsqueda, detalle, edición,
desactivación, reactivación e historial de compras a proveedores. Continúa
sobre la etapa 4.3, conservando los cuatro proyectos, .NET Framework 4.8,
ASP.NET MVC 5, Bootstrap 5 y PostgreSQL 18.

## Relación entre los archivos

```text
Formulario → ProveedoresController → ServicioProveedores → ProveedoresRepositorio
                                                              ↓
                                                          PostgreSQL
Navegador ← vistas Razor ← entidades y resultados de consulta
```

- **Entidades — `Proveedor.cs`:** `SolicitudProveedor` define los campos que
  se guardan; `Proveedor` representa el registro consultado. Las otras clases
  transportan el listado paginado y las compras del historial.
- **Datos — `ProveedoresRepositorio.cs`:** consulta y guarda con parámetros SQL.
  Usa una transacción por operación, bloqueo de fila al editar y auditoría dentro
  de la misma transacción. Las lecturas usan una instantánea coherente para que
  los totales y las filas correspondan al mismo momento.
- **Negocio — `ServicioProveedores.cs`:** verifica permisos y valida nombre,
  RUC y contacto. Comprueba la versión antes de editar o desactivar y aplica
  el permiso adicional para consultar compras.
- **Web — `ProveedoresController.cs`:** recibe solicitudes y llama al servicio.
  `ViewModels/ProveedorModelos.cs` describe los campos del formulario y sus
  mensajes de validación.
- **Vistas — `Views/Proveedores/`:** `Index.cshtml` muestra búsquedas, estados y
  páginas; `Formulario.cshtml` permite crear o editar; `Detalle.cshtml` consulta
  los datos; `Historial.cshtml` muestra compras confirmadas. Razor codifica el
  texto presentado. Los archivos nuevos usan UTF-8 con BOM para las tildes.
- **Integración:** `Seguridad/Aplicacion.cs` crea el servicio con la conexión
  privada existente, `App_Start/RouteConfig.cs` registra `/Proveedores` antes
  de FriendlyUrls y `Views/Shared/_Layout.cshtml` agrega el enlace según el
  permiso. Los archivos nuevos se incluyen en los `.csproj` existentes.

**No requiere una migración SQL nueva ni paquetes NuGet adicionales.** Se
utilizan las tablas e índices existentes y se conserva la configuración privada.

## Datos y reglas

| Campo | Límite | Regla |
| --- | ---: | --- |
| Nombre o razón social | 150 | Obligatorio |
| RUC | 40 | Opcional; único entre los registros que lo tienen |
| Teléfono | 40 | Opcional; conserva su formato |
| Dirección | 250 | Opcional; una línea |
| Correo electrónico | 254 | Opcional; una dirección sin nombre adicional |

Se recortan espacios al principio/final, se normaliza Unicode NFC y se rechazan
saltos de línea y caracteres de control. Los opcionales vacíos se guardan como
NULL. El RUC se conserva como texto, incluidos ceros iniciales y guiones; no se
convierte a número. Su unicidad ignora mayúsculas y espacios iniciales/finales,
incluso para proveedores inactivos. Puede haber varios proveedores sin RUC.
El nombre por sí solo no es un identificador único.

El listado busca por parte del nombre o RUC y filtra Todos/Activos/Inactivos.
Usa páginas de 20 registros con orden estable por nombre e ID. `%` y `_` son
comodines de PostgreSQL; las entradas siempre se envían como parámetros SQL.

Desactivar conserva el ID, RUC, contacto y documentos asociados. Para reactivar,
abre Editar, marca Activo y guarda. No hay eliminación física desde la aplicación.
El RUC sigue reservado mientras permanezca asociado al registro inactivo.

La edición y desactivación comparan `xmin`, la versión de la fila. Si otra
ventana guardó primero, se rechaza el formulario antiguo sin sobrescribir el
cambio. Vuelve al listado y abre Editar nuevamente para obtener la versión actual.
Crear, editar y desactivar registran actor, acción e ID en auditoría dentro de
la misma transacción. Una operación rechazada no registra un éxito parcial.

## Permisos e historial

- **PROVEEDORES_ADMINISTRAR:** acceso al módulo, listado, detalle y escrituras.
- **PROVEEDORES_ADMINISTRAR + COMPRAS_ADMINISTRAR:** acceso al historial.

Administrador y Supervisor tienen inicialmente ambos permisos. Cajero no tiene
acceso al módulo con su rol inicial. Los roles personalizados se comprueban en
cada operación. Administrar datos del proveedor no basta para ver importes de
compras: también hace falta el permiso de compras. MVC protege las rutas y el
servicio repite la validación. Guardar y Desactivar son POST con antifalsificación.
Se conserva la configuración de autenticación y HTTPS existente.

El historial consulta la tabla **compras** y muestra solo documentos CONFIRMADOS
asociados al proveedor. Presenta registro interno, número de documento si existe,
fecha en horario de Paraguay, usuario e importe, con 20 filas por página. El total
acumulado incluye todas las compras confirmadas del proveedor y no los borradores.
El registro interno no sustituye el número del documento del proveedor.

Si todavía no hay compras registradas y confirmadas, la pantalla estará vacía.
Esta entrega no registra compras ni entradas de mercadería al consultar el
historial. El registro transaccional de compras se implementará en su etapa.
Editar o desactivar un proveedor no cambia sus documentos ni el inventario.

## Incorporar en tu notebook cuando la rama esté publicada

1. Detén la aplicación en Visual Studio. Comprueba `git status --short` en
   `D:\Sistemas\SistemaVentas` y conserva los cambios locales antes de actualizar.
2. Incorpora la rama de la entrega siguiendo los comandos indicados en el chat.
   No recrees la base ni repitas migraciones anteriores. Esta etapa no tiene
   un script SQL de instalación nuevo.
3. Abre la solución de esa misma carpeta, restaura NuGet si Visual Studio lo
   solicita y recompila. Conserva `AppSettings.Local.config` fuera de Git.
4. Inicia por HTTPS e ingresa como administrador. El menú tendrá **Proveedores**.
   No se crean cuentas ni contraseñas predeterminadas.

## Recorrido de revisión en Windows

1. Abre Proveedores. Si no tenías registros anteriores, el listado estará vacío.
2. Registra un proveedor de prueba con nombre, RUC, teléfono, dirección y correo.
   Usa un RUC que no esté registrado. Revisa los valores y tildes en el detalle.
3. Busca por parte del nombre y del RUC. Intenta registrar otro con el mismo RUC
   usando espacios al principio/final: debe rechazarlo. Prueba nombre vacío,
   correo mal formado y campos demasiado largos.
4. Registra otro proveedor dejando los opcionales vacíos. El nombre sigue siendo
   obligatorio. Comprueba que no se inventen RUC ni datos de contacto.
5. Edita teléfono o dirección. Abre el mismo registro para editar en dos pestañas:
   guarda en la primera e intenta guardar en la segunda. Debe rechazar la versión
   antigua sin sobrescribir los datos.
6. Desactiva el proveedor de prueba, filtra Inactivos y revisa su ficha. Intenta
   duplicar su RUC: sigue reservado. Reactiva desde Editar; debe conservar su ID.
7. Prueba búsqueda, filtros y paginación si tienes más de 20 proveedores, además
   de la presentación de formularios y tablas en una ventana estrecha.
8. Abre Historial de compras. Si no hay compras confirmadas, estará vacío.
   Si tienes documentos anteriores, deben corresponder al proveedor y conservar
   sus importes después de editar o desactivar la ficha.
9. Con una cuenta Cajero sin permisos adicionales, no debe aparecer el enlace
   del menú y `/Proveedores` debe responder 403. Con un rol personalizado que
   solo administre proveedores, Historial debe responder 403 sin permiso de compras.
10. Comprueba que Clientes, Productos, Categorías, Marcas, Usuarios, Roles,
    Inicio y Cerrar sesión sigan funcionando según sus permisos.

## Validación automatizada y límites

`tests/proveedores/ProveedoresPruebas.cs` usa PostgreSQL 18 real en una base
independiente y descartable. Comprueba RUC, contacto, permisos, auditoría,
búsquedas, páginas, reactivación y conflictos concurrentes. También prepara
compras completas con detalles, movimientos coherentes y auditoría para
comprobar el historial, sus totales y la exclusión de borradores. Esa preparación
solo se ejecuta en la base de prueba: no implementa el servicio de compras y no
se aplica en tu notebook. Los filtros MVC usan contextos de prueba, no peticiones
HTTP completas.

```bash
python3 tests/seguridad/ejecutar.py --proveedores \
  --dotnet /ruta/al/dotnet \
  --referencias /ruta/al/paquete/net48/build/.NETFramework/v4.8
```

Se necesitan los paquetes restaurados en `packages/`, las referencias oficiales
net48 y Docker con `postgres:18` y `mono:6.12`. El ejecutor crea y elimina la
base temporal con credenciales aleatorias y aplica allí las migraciones
anteriores como preparación. No conecta al PostgreSQL de tu notebook.

Resultados de esta entrega en el entorno de nube:

- Cuatro proyectos compilados contra .NET Framework 4.8.
- Las 23 vistas Razor procesadas con MVC 5.3 y compiladas contra net48.
- 56 comprobaciones de proveedores correctas sobre PostgreSQL 18.
- 60 comprobaciones de clientes correctas como regresión.
- 58 comprobaciones de seguridad correctas como regresión.
- Los archivos existentes de `database/` se conservaron sin cambios.

Se intentó abrir la aplicación con Chromium y un host ASP.NET temporal en
Mono 6.12. El arranque falla porque Mono no resuelve
`System.Web.UI.ScriptResourceDefinition` de `System.Web.Extensions`. Esto
impide validar las pantallas completas en este entorno Linux; la compilación
de Razor y las pruebas de servicios no sustituyen ese recorrido.

El recorrido de pantallas en .NET Framework 4.8 e IIS Express queda pendiente
en Windows antes de cerrar esta entrega y avanzar a la etapa del POS.
