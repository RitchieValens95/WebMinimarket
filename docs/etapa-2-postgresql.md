# Etapa 2: conexión con PostgreSQL 18

La instalación de Windows ya respondió con PostgreSQL 18.6. Esta etapa prepara
una base de desarrollo y comprueba la conexión desde las capas existentes.
No crea tablas ni registra ventas.

## Archivos y recorrido

`/Inicio/Conexion` → `InicioController.Conexion` →
`DiagnosticoConexion.Comprobar` (Negocio) → `ConexionPostgreSql.Comprobar` (Datos)
→ PostgreSQL → `ResultadoConexion` (Entidades) → `Views/Inicio/Conexion.cshtml`.

- Datos abre la conexión, ejecuta `SELECT version(), current_setting('server_version_num')::integer`
  y libera los recursos con `using`. Limita la apertura a 5 segundos y la consulta
  a 10 segundos. Devuelve mensajes controlados sin la cadena de conexión.
- Negocio comprueba que la versión principal sea 18.
- Web lee la configuración local. El diagnóstico solo se habilita si la petición
  es local y ASP.NET está en modo de depuración. Fuera de ese contexto devuelve 404.
- Entidades transporta el resultado sin depender de Npgsql.

La página de inicio MVC y las páginas Web Forms se conservan.

## Crear el usuario y la base en pgAdmin 4

Conéctate como `postgres` para estas tareas de administración:

1. En **Login/Group Roles → Create → Login/Group Role**, crea `sistemaventas_app`.
2. En **Definition**, define su propia contraseña y guárdala en tu equipo.
3. En **Privileges**, activa **Can login?**. Deja desactivados **Superuser**,
   **Create roles** y **Create databases**.
4. En **Databases → Create → Database**, crea `sistemaventas` y selecciona como
   propietario (**Owner**) a `sistemaventas_app`.

Para verificar el usuario, registra otra conexión en pgAdmin con estos valores:

| Campo | Valor |
| --- | --- |
| Nombre de la conexión | SistemaVentas local |
| Host | localhost |
| Puerto | 5432, o el elegido al instalar |
| Maintenance database | sistemaventas |
| Username | sistemaventas_app |
| Password | La contraseña de ese usuario |

Abre Query Tool desde esa conexión y ejecuta:

```sql
SELECT current_database(), current_user, version();
```

Debe devolver `sistemaventas`, `sistemaventas_app` y PostgreSQL 18.x.
No uses el administrador `postgres` en la configuración de la aplicación.
Estos pasos son para una base nueva; si los nombres ya existen, revisa los objetos
existentes y sus propietarios antes de repetirlos. No los elimines.

## Obtener esta etapa desde GitHub

Los cambios se entregan en la rama `codex/etapa2-postgresql`. No hace falta
descargar un parche. Cierra Visual Studio y comprueba tu copia de `D:`:

```powershell
git -C "D:\Sistemas\SistemaVentas" status --short
```

Si hay cambios pendientes, detente y consérvalos antes de continuar. Si la salida
está vacía, descarga la rama y avanza tu rama local sin reescribir el historial:

```powershell
git -C "D:\Sistemas\SistemaVentas" fetch origin
git -C "D:\Sistemas\SistemaVentas" merge --ff-only origin/codex/etapa2-postgresql
```

Ejecuta cada comando por separado. Si alguno falla, detente y comparte el mensaje.
El envío de la rama no modifica `main` en GitHub; cuando la etapa quede validada
podrás publicar el avance local de `main` con un push normal.

## Configurar la conexión local

`Web.config` contiene `appSettings file="AppSettings.Local.config"`.
El archivo externo es opcional: su ausencia no impide abrir el inicio.
`AppSettings.Local.example.config` es una plantilla sin credenciales reales.

El script siguiente pide la contraseña sin mostrarla y genera la configuración.
Usa generadores de cadena y XML para admitir caracteres especiales sin tener
que escaparlos manualmente. No sobrescribe un archivo existente salvo que se
indique expresamente `-Sobrescribir`.

```powershell
cd "D:\Sistemas\SistemaVentas"
.\tools\Configurar-ConexionLocal.ps1
```

Si Windows bloquea la ejecución del script por su política, puedes ejecutarlo
con una política limitada a ese proceso, después de revisar el script:

```powershell
powershell -NoProfile -ExecutionPolicy RemoteSigned -File .\tools\Configurar-ConexionLocal.ps1
```

Si usas otro puerto, añade `-Puerto 5433` sustituyendo el número por el tuyo.
El archivo generado contiene la contraseña en texto y debe permanecer solo
en tu equipo. No lo compartas ni lo subas. Está excluido mediante `.gitignore`.

Comprueba la exclusión:

```powershell
git check-ignore SistemaVentas.Web/AppSettings.Local.config
```

Debe imprimir esa ruta. Si no lo hace, no agregues el archivo a Git.
El archivo local tampoco se incluye en el `.csproj` ni se distribuye en GitHub.
Para un servidor remoto se debe configurar TLS con verificación del certificado;
el ejemplo de esta etapa corresponde al servidor de desarrollo local.

## Restaurar, compilar y probar

1. Abre `D:\Sistemas\SistemaVentas\SistemaVentas.slnx`.
2. Restaura los paquetes NuGet de la solución y compila en **Debug**.
3. Inicia `SistemaVentas.Web` con IIS Express.
4. Conserva el host y puerto que abra Visual Studio y visita `/Inicio/Conexion`.
5. Debe aparecer **Conexión correcta con PostgreSQL 18** y la versión del servidor.
6. Verifica también `/Inicio/Index`, `/About` y `/Contact`.

Si falta el archivo local, la pantalla debe indicar que falta configurar la
conexión. Si el servidor está detenido, la contraseña es incorrecta o la base no
existe, mostrará un mensaje controlado. No compartas el archivo privado para
diagnosticar errores: basta con el mensaje de la pantalla o de compilación.

## Dependencias y límites de validación

Todos los proyectos siguen apuntando a .NET Framework 4.8. Npgsql 4.1.14 incluye
un ensamblado `net461` utilizable desde 4.8. Es una rama antigua de Npgsql:
la compatibilidad de esta prueba no equivale a soporte vigente del proveedor.
No se migra la solución a .NET moderno en esta etapa.

Las dependencias se fijan en `packages.config` de Datos y del host Web porque
ese formato no instala dependencias transitivas automáticamente al restaurar.
Web incluye referencias y redirecciones de versiones para cargar las mismas
bibliotecas al ejecutar con IIS. System.ValueTuple está incluido en .NET Framework
4.8, por lo que su paquete se registra sin añadir una DLL redundante al proyecto.

La ejecución definitiva en Windows/IIS Express debe comprobarse en tu equipo.

## Evidencia de esta etapa en el entorno de desarrollo

- Compilación directa del C# de los cuatro proyectos con Roslyn y las referencias
  oficiales de .NET Framework 4.8: sin errores.
- Ejecución de las bibliotecas resultantes en Mono 6.12 contra un contenedor
  oficial PostgreSQL 18.6 con autenticación SCRAM y un usuario sin privilegios
  de administrador: conexión y consulta correctas.
- 17 comprobaciones de conexión/configuración: incluyen contraseña incorrecta,
  base inexistente, puerto sin servidor, formato inválido, clave desconocida,
  cadena vacía y ausencia del archivo local. Mensajes sin credenciales.
- 3 comprobaciones adicionales del controlador: rechazo de peticiones remotas,
  rechazo fuera de depuración y resultado controlado sin configuración local.
- Generación de la vista tipada con el host MVC 5.3.0 y compilación del C# generado
  contra .NET Framework 4.8: sin errores.
- Archivo privado excluido por Git, XML válido y archivos declarados presentes.

Mono no sustituye la validación con .NET Framework 4.8 en Windows. El script
PowerShell se revisó, pero su ejecución en Windows queda pendiente junto con
la restauración/compilación completa de Visual Studio y las pruebas HTTP bajo IIS.
