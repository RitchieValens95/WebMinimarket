# WebMinimarket
Sistema web con codex utilizando C#

La solución **SistemaVentas** conserva cuatro proyectos: Web, Entidades, Negocio
y Datos. Utiliza .NET Framework 4.8, ASP.NET MVC 5, Bootstrap 5 y PostgreSQL 18
con ADO.NET/Npgsql.

## Documentación

- [Requisitos completos y etapas originales](docs/requisitos-originales.md).
- [Arquitectura y diseño de datos](docs/diseno-sistema-ventas.md).
- [Pantalla inicial MVC y ejecución en Visual Studio](docs/etapa-1-mvc.md).
- [Preparación de la conexión local con PostgreSQL](docs/etapa-2-postgresql.md).
- [Etapa 2 original: crear y comprobar tablas y datos iniciales](database/README.md).
- [Etapa 3: acceso, usuarios, roles y permisos](docs/etapa-3-seguridad.md).

La base técnica, la conexión y el esquema de la etapa 2 se validaron en Windows.
La etapa 3 incorpora acceso y administración de usuarios/roles, con pruebas en
PostgreSQL temporal y pendiente de validación final bajo IIS Express. El catálogo,
POS, compras, caja y reportes siguen las etapas siguientes de los requisitos.

La ejecución de la aplicación requiere Windows y las herramientas de desarrollo
de .NET Framework 4.8. El SDK de .NET moderno no sustituye ese entorno. La
configuración privada de conexión permanece excluida de Git.
