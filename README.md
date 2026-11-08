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
- [Etapa 4.1: categorías y marcas](docs/etapa-4-1-catalogos.md).
- [Etapa 4.2: productos e historial de stock](docs/etapa-4-2-productos.md).
- [Etapa 4.3: clientes e historial de compras](docs/etapa-4-3-clientes.md).

La base técnica, la conexión y el esquema de la etapa 2 se validaron en Windows.
El acceso y los módulos anteriores de categorías, marcas y productos fueron
revisados por el usuario en Windows. La etapa 4.3 incorpora clientes e historial
de compras según permisos, con pruebas sobre PostgreSQL temporal y pendiente
del recorrido bajo IIS Express. Proveedores, POS, compras, caja y reportes
siguen las siguientes entregas.

La ejecución de la aplicación requiere Windows y las herramientas de desarrollo
de .NET Framework 4.8. El SDK de .NET moderno no sustituye ese entorno. La
configuración privada de conexión permanece excluida de Git.

La corrección de productos incorpora IVA incluido (Exento, 5 %, 10 %) y EAN-13
interno con prefijo 28 cuando no hay barra de fabricante. Requiere aplicar
`database/008_producto_iva_ean13.sql` y verificar con
`database/009_comprobar_producto_iva_ean13.sql`; los pasos están en la guía 4.2.

La ampliación de balanza agrega Pesable (20) y Unitario (21), con prefijo fijo
de siete dígitos y lectura de etiquetas completas 2+5+5+1. Sobre 008 validada,
aplica `010_productos_balanza.sql` y verifica con
`011_comprobar_productos_balanza.sql`, siguiendo la misma guía 4.2.
