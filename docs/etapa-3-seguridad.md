# Etapa 3: acceso, usuarios, roles y permisos

Esta etapa sigue el prompt original y conserva los cuatro proyectos, .NET
Framework 4.8, ASP.NET MVC 5 y PostgreSQL 18. Los módulos comerciales de las
etapas posteriores todavía no se implementan.

## Archivos y relación entre capas

| Archivo o carpeta | Responsabilidad |
| --- | --- |
| `SistemaVentas.Entidades/Seguridad.cs` | Solicitudes, resultados, roles, permisos y sesión sin contraseñas. La credencial con hash solo se usa entre Datos y Negocio. |
| `SistemaVentas.Datos/RepositorioSeguridad.cs` | Consultas parametrizadas, usuarios, roles, permisos, intentos y auditoría. Una conexión y transacción por operación. |
| `SistemaVentas.Negocio/Contrasenas.cs` | Hash y verificación PBKDF2-HMAC-SHA256. |
| `SistemaVentas.Negocio/ServicioSeguridad.cs` | Validaciones, ingreso, administración, autorización y protección del último administrador. |
| `SistemaVentas.Web/Seguridad/` | Cookies, sesión, filtros de autorización, errores controlados, límite de accesos y presentación de fechas. |
| `AccesoController`, `UsuariosController`, `RolesController` | Reciben formularios y llaman a Negocio; no contienen SQL. |
| `SistemaVentas.Web/ViewModels/SeguridadModelos.cs` | Modelos y validaciones de los formularios. |
| `SistemaVentas.Web/Views/Acceso`, `Usuarios`, `Roles`, `Shared` | Ingreso y administración con Bootstrap 5 y navegación adaptable a permisos. |
| `database/004_seguridad.sql` | Nuevas columnas de sesión/intentos y un índice para búsqueda de usuarios. |
| `database/005_comprobar_seguridad.sql` | Comprobación de solo lectura de esta etapa. |
| `tools/Crear-PrimerAdministrador.ps1` y `AdministradorInicial.cs` | Creación local del primer administrador, sin contraseña predeterminada ni endpoint público de instalación. |
| `tests/seguridad/` | Compilación net48, pruebas reales en PostgreSQL y pruebas de filtros y Razor. |

El recorrido de una solicitud autenticada es: cookie protegida → filtro de
sesión → usuario activo y permisos actuales en PostgreSQL → controlador →
servicio de Negocio → repositorio → vista. La sesión Web se resuelve una vez por
solicitud; Negocio vuelve a autorizar las operaciones sensibles dentro de su
transacción. Los permisos no se guardan indefinidamente en Session ni se confían
al navegador.

## Aplicación en Windows cuando dispongas de estos cambios

1. Conserva tu `AppSettings.Local.config` privado. No reemplaces su contraseña
   ni lo subas a Git. Detén la aplicación antes de actualizar archivos.
2. En pgAdmin, abre Query Tool en **sistemaventas**, como **sistemaventas_app**.
3. Ejecuta completo **solo `database/004_seguridad.sql`**. No vuelvas a aplicar
   001 ni 002 en tu base ya validada. Si ya tienes datos propios, conserva un
   respaldo de la base antes de aplicar la nueva migración.
4. Ejecuta `database/005_comprobar_seguridad.sql`; debe finalizar con aviso de
   estructura de seguridad correcta y una fila `OK`. Antes del primer administrador
   es normal que indique cero usuarios. El verificador 003 actualizado también
   contempla las nuevas restricciones, pero no es necesario repetirlo aquí.
5. Abre la solución de tu carpeta de trabajo en Visual Studio. Restaura NuGet
   si es necesario y compila toda la solución en **Debug**.
6. Crea el primer administrador como se explica a continuación.
7. Ejecuta Web con IIS Express y abre su dirección **HTTPS**. El puerto es el
   configurado en Visual Studio; no presupongas otro puerto.

Forms Authentication necesita que IIS Express permita solicitudes anónimas y
que la autenticación de Windows esté deshabilitada; el ingreso lo controla la
aplicación. Es la configuración habitual de este proyecto.

La migración agrega `version_sesion`, `intentos_fallidos` y `bloqueado_hasta` a
usuarios. No modifica hashes ni crea contraseñas, no elimina registros y registra
su checksum en `esquema_versiones`. Repetir la misma migración no reinicia sesiones
ni contadores. Si falla, detente y comparte el mensaje sin credenciales. Si Query
Tool quedó en una transacción abortada, ejecuta `ROLLBACK;` antes del diagnóstico.

## Crear el primer administrador

Abre **Windows PowerShell 5.1** o Developer PowerShell de Visual Studio, no
Package Manager Console ni PowerShell 7, y ejecuta:

```powershell
cd "D:\Sistemas\SistemaVentas"
.\tools\Crear-PrimerAdministrador.ps1
```

Si estás usando otra carpeta, sustituye la ruta por la de esa solución. Si una
política impide ejecutar el script local, después de revisarlo puedes usar una
política limitada a ese proceso, sin cambiar la política global:

```powershell
powershell -NoProfile -ExecutionPolicy RemoteSigned -File .\tools\Crear-PrimerAdministrador.ps1
```

El script compila una pequeña herramienta con .NET Framework y las DLL del
proyecto. Las salidas quedan en `tools/bin/administrador-inicial`, excluido de
Git. La herramienta lee tu archivo local, solicita usuario, nombre y dos veces
una contraseña de 12 a 128 caracteres. La contraseña no se muestra ni se recibe
como argumento de consola. No hay un valor inicial compartido.

La cuenta y su rol Administrador se registran en una única transacción, junto
con auditoría. Si ya existe cualquier usuario, se rechaza repetir la instalación.
No borres cuentas para forzar su ejecución; la recuperación de una instalación
con usuarios se debe revisar por separado.

## Funcionamiento de la seguridad

- Hash versionado `v1$pbkdf2-sha256$600000$sal$hash`, sal aleatoria de 16 bytes y
  resultado de 32 bytes. Comparación sin salida temprana. No se registra la
  contraseña ni el hash en auditoría, cookies o vistas.
- El usuario de acceso admite de 3 a 80 letras ASCII, números, punto, guion y
  guion bajo. El nombre de la persona sí admite acentos y otros caracteres.
- Después de cinco fallos, la cuenta queda bloqueada durante 15 minutos.
  Un acceso correcto reinicia el contador. Al cambiar su contraseña, un
  administrador también reinicia el bloqueo. La respuesta de rechazo no revela
  si la cuenta existe, está inactiva o su contraseña no coincide.
- Límite adicional de 20 envíos de ingreso por minuto y dirección IP en la
  instancia web. Es un límite en memoria del proceso; el bloqueo por cuenta
  reside en PostgreSQL y se comparte entre instancias.
- Forms Authentication protege un ticket con el id de usuario y su versión de
  sesión. Nunca incluye contraseña, hash ni una lista de permisos confiada al cliente.
- Cookies HttpOnly, Secure y SameSite=Lax; formulario CSRF también requiere HTTPS.
  El acceso dura 30 minutos, sin renovación deslizante ni opción persistente.
  El plazo se toma de la configuración de Forms Authentication en Web.config.
- Cerrar sesión invalida las sesiones de esa cuenta en el servidor y borra las
  cookies locales. Si la base no está disponible, se borran igualmente las cookies
  del navegador; no puede confirmarse la revocación remota hasta recuperar la base.
- Editar un usuario invalida sus sesiones previas; al desactivarlo no puede volver
  a entrar. Si editas tu propia cuenta, deberás iniciar sesión nuevamente.
- Cambiar un rol o sus permisos se refleja en la siguiente solicitud. Desactivar
  un rol deja de conceder sus permisos, aunque el usuario conserve otros roles.
- Las operaciones de administración se serializan con un bloqueo transaccional.
  No se permite dejar al sistema sin un usuario activo con el rol Administrador
  activo y los permisos USUARIOS_VER, USUARIOS_ADMINISTRAR y ROLES_ADMINISTRAR.
- Usuarios inactivos se conservan para mantener su historial. Los roles nuevos y
  sus permisos se gestionan desde la pantalla de roles. Los códigos de roles
  existentes se mantienen estables. Los permisos disponibles son el catálogo de
  acciones definidas por la aplicación, no nombres arbitrarios introducidos en UI.
- Se auditan creación/edición/desactivación de usuarios, roles, administrador
  inicial e ingresos/salidas. Los intentos durante un bloqueo no lo prolongan.
- SQL siempre parametrizado, validación en servidor y tokens CSRF en los POST.
  Los errores técnicos devuelven mensajes controlados y registran solo el tipo
  de error, sin exponer SQL o cadenas de conexión.
- Se conserva Web Forms como plantillas previas; requiere autenticación. Los
  módulos con permisos se implementan en MVC. `/Inicio/Conexion` requiere además
  permiso de administración de usuarios, solicitud local y modo de depuración.
- Los últimos accesos se muestran según Paraguay, usando el identificador de zona
  de Windows y el equivalente America/Asuncion en otros entornos. El sistema
  operativo debe tener actualizadas sus reglas horarias.

Usuarios tiene búsqueda y paginación SQL de 20 filas. Roles se filtra y pagina
en el servidor sobre su catálogo, normalmente pequeño. La interfaz muestra
solo las acciones permitidas, pero los filtros y Negocio también las verifican.

## Pruebas manuales en Visual Studio/IIS Express

| Paso | Resultado esperado |
| --- | --- |
| Abrir `/` sin autenticar | Redirección a `/Acceso/Ingresar`. |
| Ingresar con la cuenta creada localmente | Inicio con tu nombre y menú Usuarios/Roles. |
| Contraseña incorrecta | Mensaje controlado; no hay ingreso. |
| Cinco contraseñas incorrectas de un usuario de prueba | Bloqueo de 15 minutos, incluso con su contraseña correcta. |
| Crear un usuario con rol Cajero | Se guarda sin contraseña en claro; no ve administración. |
| Abrir `/Usuarios` o `/Roles` con Cajero | HTTP 403; el menú oculto no es la única protección. |
| Editar usuario dejando contraseña vacía | Conserva la contraseña anterior e invalida sus sesiones anteriores. |
| Asignar contraseña nueva | Funciona la nueva y deja de funcionar la anterior. |
| Desactivar un usuario con sesión iniciada en otro navegador | La siguiente solicitud pierde acceso y no puede volver a entrar. |
| Modificar permisos de un rol personalizado | Se aplican en la siguiente solicitud del usuario de ese rol. |
| Desactivar al último administrador o quitarle permisos básicos | Mensaje controlado y cambios revertidos. |
| Buscar y pasar de página | Resultados y totales provenientes de PostgreSQL. |
| Cerrar sesión y volver con el botón Atrás | La siguiente solicitud protegida exige entrar; las respuestas no se almacenan en caché. |
| Usar una URL de retorno de otro sitio | Después de entrar se dirige al inicio, no al sitio externo. |
| POST sin token CSRF válido | HTTP 400; sin modificación de datos. |
| HTTP sin TLS | No se envía la cookie de acceso ni se permite ingresar. Usa HTTPS. |

Para probar dos cuentas utiliza un navegador normal y otro privado, sin compartir
las credenciales. No uses al único administrador para pruebas de bloqueo. No
compartas contraseñas ni el archivo local al informar errores.

## Validación automatizada y límites

En Linux con Docker, NuGet restaurado, SDK con Roslyn y referencias oficiales
de .NET Framework 4.8:

```bash
python3 tests/seguridad/ejecutar.py --dotnet /ruta/dotnet --referencias /ruta/referencias/net48
```

`--solo-vistas` permite compilar y comprobar Razor sin crear una base. El ejecutor
completo crea un PostgreSQL 18 temporal con credenciales aleatorias, prepara allí
las tablas anteriores como fixture, aplica 004 y prueba su repetición y checksum.
No conecta a tu base de Windows. El contenedor se elimina al terminar.

Se comprobaron 58 casos de negocio y filtros en Mono/PostgreSQL 18.6, incluidos
hashes, intentos, permisos, transacciones, inyección SQL, protección del último
administrador con concurrencia, invalidación de sesiones, paginación y filtros.
Las nueve vistas se generan con el host MVC 5.3.0 y su C# se compila contra net48.
También se compila el ejecutable de administrador con compatibilidad C# 5.

Estas pruebas no equivalen a navegación completa bajo IIS: los filtros HTTP
se prueban con contextos de prueba y los servicios usan PostgreSQL real. La
emisión de cookies, formularios CSRF y navegación deben validarse en Windows.
El script PowerShell y la entrada interactiva de consola quedan pendientes de
ejecución en tu equipo. Esta etapa no se considerará validada allí hasta completar
las comprobaciones relevantes de la tabla anterior.
