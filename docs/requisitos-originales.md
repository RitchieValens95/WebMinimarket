# Desarrollo de un sistema de ventas web con C# .NET Framework y PostgreSQL

Actúa como un desarrollador senior especializado en C#, ASP.NET MVC, .NET Framework, PostgreSQL, arquitectura de software y desarrollo de sistemas de gestión comercial.

Necesito que desarrolles un **sistema de ventas web completo**, orientado a la administración de un comercio, minimarket o autoservicio.

El sistema debe ser funcional, tener una interfaz moderna, intuitiva y adaptable a computadoras, tablets y dispositivos móviles.

## 1. Tecnologías obligatorias

Utiliza las siguientes tecnologías:

- **Lenguaje:** C#.
- **Framework:** .NET Framework 4.8.
- **Aplicación web:** ASP.NET MVC 5.
- **Frontend:** HTML5, CSS3, JavaScript y Bootstrap 5.
- **Base de datos:** PostgreSQL.
- **Conexión a PostgreSQL:** Npgsql.
- **Acceso a datos:** Entity Framework 6 o ADO.NET, seleccionando la alternativa más conveniente y manteniendo una arquitectura organizada.
- **IDE:** Visual Studio.
- **Arquitectura:** Separación por capas (Presentación, Negocio y Acceso a Datos).

Importante: No utilizar .NET Core ni ASP.NET Core. El proyecto debe ser compatible con .NET Framework 4.8.

## 2. Módulos principales

### A. Inicio de sesión y usuarios

- Pantalla de inicio de sesión.
- Administración de usuarios.
- Registro de usuarios.
- Edición y desactivación de usuarios.
- Roles y permisos.
- Control de acceso a los módulos según el rol.
- Registro de fecha y hora del último acceso.

Roles iniciales:

- Administrador.
- Supervisor.
- Cajero.

### B. Panel principal (Dashboard)

Mostrar información resumida del negocio:

- Total de ventas del día.
- Cantidad de ventas realizadas.
- Productos con stock bajo.
- Productos agotados.
- Total de ingresos.
- Últimas ventas realizadas.
- Gráficos de ventas por día, semana y mes.

### C. Gestión de productos

Permitir:

- Registrar productos.
- Editar productos.
- Desactivar productos.
- Buscar productos por descripción, código o código de barras.
- Asignar categorías.
- Asignar marcas.
- Definir precio de compra.
- Definir precio de venta.
- Controlar existencia.
- Definir stock mínimo.
- Permitir productos vendidos por unidad, peso o cantidad decimal.
- Registrar códigos de barras.
- Consultar historial de movimientos de stock.

La búsqueda debe ser rápida y permitir utilizar lectores de códigos de barras.

**Importante:** El sistema debe soportar productos con cantidades decimales, por ejemplo: 0,250 kg, 1,500 kg o 2,750 unidades cuando corresponda.

### D. Gestión de clientes

- Registrar clientes.
- Editar información.
- Buscar clientes por nombre, documento o RUC.
- Consultar historial de compras.
- Registrar teléfono, dirección y correo electrónico.

Debe existir un cliente genérico para ventas al contado que no requieran identificar al comprador.

### E. Punto de venta (POS)

Este será uno de los módulos más importantes del sistema.

Desarrollar una pantalla de ventas que permita:

- Buscar productos por descripción.
- Buscar productos por código de barras.
- Agregar productos mediante lector de códigos de barras.
- Modificar cantidades.
- Permitir cantidades decimales.
- Modificar productos antes de confirmar la venta.
- Eliminar productos del detalle.
- Calcular subtotales automáticamente.
- Aplicar descuentos.
- Calcular el total de la venta.
- Seleccionar un cliente.
- Seleccionar el método de pago.
- Confirmar y registrar la venta.

Métodos de pago iniciales:

- Efectivo.
- Tarjeta.
- Transferencia.
- Otros.

La pantalla debe ser rápida y sencilla, evitando pasos innecesarios para el cajero.

Al confirmar una venta:

1. Registrar la cabecera de la venta.
2. Registrar los detalles de los productos vendidos.
3. Actualizar automáticamente el stock.
4. Registrar el movimiento de salida de mercadería.
5. Registrar el pago.
6. Generar un número correlativo de venta.
7. Permitir imprimir un ticket.

Todas las operaciones relacionadas con la confirmación de una venta deben ejecutarse dentro de una transacción de base de datos para evitar inconsistencias.

### F. Gestión de compras

- Registrar compras a proveedores.
- Agregar productos al detalle.
- Registrar cantidades y costos.
- Calcular el total de la compra.
- Actualizar automáticamente el stock al confirmar la compra.
- Consultar historial de compras.

### G. Gestión de proveedores

- Registrar proveedores.
- Editar información.
- Desactivar proveedores.
- Buscar proveedores.
- Consultar compras realizadas a cada proveedor.

### H. Control de stock

- Consultar stock actual.
- Registrar entradas de mercadería.
- Registrar salidas de mercadería.
- Realizar ajustes de stock.
- Consultar historial de movimientos.
- Identificar productos con stock mínimo.
- Identificar productos agotados.

Cada movimiento debe guardar fecha, usuario responsable, tipo de movimiento, cantidad y observación.

### I. Caja

- Apertura de caja.
- Registro de monto inicial.
- Registro de ingresos y egresos.
- Asociación de ventas a la caja abierta.
- Cierre de caja.
- Cálculo de efectivo esperado.
- Registro de efectivo contado.
- Identificación de diferencias.
- Historial de aperturas y cierres.

### J. Reportes

Desarrollar reportes con filtros por fecha:

- Ventas diarias.
- Ventas por período.
- Ventas por producto.
- Ventas por categoría.
- Ventas por usuario.
- Productos más vendidos.
- Compras realizadas.
- Movimientos de stock.
- Ganancias estimadas.
- Cierres de caja.

Permitir exportar los reportes a Excel y PDF.

## 3. Diseño de la interfaz

Quiero una interfaz moderna y profesional.

Características:

- Menú lateral de navegación.
- Dashboard con tarjetas informativas.
- Tablas con búsqueda, filtros y paginación.
- Formularios sencillos.
- Ventanas modales para operaciones rápidas.
- Diseño responsive.
- Colores profesionales.
- Iconografía consistente.
- Notificaciones de éxito y error.
- Confirmaciones antes de eliminar o cancelar operaciones.

El módulo POS debe tener una interfaz independiente, optimizada para pantallas de computadora y uso con teclado y lector de códigos de barras.

## 4. Base de datos PostgreSQL

Diseñar una base de datos relacional bien estructurada.

Crear las tablas necesarias, incluyendo como mínimo:

- usuarios
- roles
- permisos
- usuarios_roles
- clientes
- proveedores
- categorias
- marcas
- productos
- productos_codigos_barras
- ventas
- ventas_detalles
- pagos
- compras
- compras_detalles
- movimientos_stock
- cajas
- cajas_movimientos
- auditoria

Requisitos:

- Utilizar claves primarias y foráneas.
- Crear índices para mejorar las búsquedas.
- Establecer restricciones de integridad.
- Utilizar NUMERIC para importes y cantidades decimales.
- Utilizar secuencias o mecanismos seguros para los correlativos.
- Definir correctamente las relaciones entre tablas.
- Evitar duplicación innecesaria de información.
- Preparar scripts SQL para crear toda la estructura.
- Crear datos iniciales de prueba.

## 5. Seguridad y validaciones

Implementar:

- Contraseñas almacenadas mediante hash seguro.
- Validación de datos en servidor.
- Validaciones en formularios.
- Protección contra SQL Injection.
- Protección CSRF.
- Control de permisos por módulo.
- Manejo de sesiones.
- Registro de operaciones importantes.
- Control de errores y excepciones.

No confiar únicamente en las validaciones JavaScript del navegador.

## 6. Arquitectura del proyecto

Organizar la solución de Visual Studio de la siguiente manera:

- SistemaVentas.Web: interfaz y controladores.
- SistemaVentas.Entidades: entidades y modelos.
- SistemaVentas.Negocio: reglas de negocio.
- SistemaVentas.Datos: conexión y operaciones con PostgreSQL.

Mantener el código limpio, reutilizable y organizado.

Evitar colocar consultas SQL directamente en los controladores.

Centralizar la configuración de conexión a PostgreSQL y utilizar parámetros para las consultas.

## 7. Reglas importantes del sistema

- No permitir confirmar ventas sin productos.
- No permitir cantidades iguales o menores a cero.
- No permitir pagos que no correspondan al total de la venta, salvo que se implemente expresamente el manejo de pagos parciales.
- Controlar correctamente el stock.
- Evitar que una misma venta se registre dos veces por doble clic.
- No permitir cerrar una caja que no esté abierta.
- Registrar las operaciones importantes para auditoría.
- Utilizar transacciones para operaciones que involucren varias tablas.
- Manejar correctamente los valores decimales y monetarios.
- Utilizar la zona horaria correspondiente a Paraguay (America/Asuncion).

## 8. Forma de trabajo

No desarrolles todo el sistema de manera desordenada ni generes archivos sin una estructura definida.

Trabaja por etapas:

**Etapa 1:** Diseño de arquitectura, creación de la solución y configuración de PostgreSQL.

**Etapa 2:** Creación de la base de datos, tablas, relaciones e información inicial.

**Etapa 3:** Inicio de sesión, usuarios, roles y permisos.

**Etapa 4:** Productos, categorías, marcas, clientes y proveedores.

**Etapa 5:** Desarrollo del punto de venta (POS).

**Etapa 6:** Compras y control de stock.

**Etapa 7:** Gestión de caja.

**Etapa 8:** Dashboard y reportes.

**Etapa 9:** Pruebas, corrección de errores y optimización.

Al finalizar cada etapa:

1. Verifica que el código compile.
2. Comprueba la conexión con PostgreSQL.
3. Realiza pruebas de las funcionalidades implementadas.
4. Corrige los errores encontrados.
5. Documenta los archivos creados y modificados.
6. Indica cómo ejecutar y probar el sistema.

No avances dejando errores importantes pendientes.

## 9. Entregables

Quiero recibir:

- Solución completa de Visual Studio.
- Código fuente organizado.
- Scripts SQL de PostgreSQL.
- Archivo de configuración de conexión.
- Datos iniciales para realizar pruebas.
- Instrucciones de instalación.
- Instrucciones para ejecutar el proyecto.
- Usuario y contraseña iniciales de administración, configurados de forma segura.
- Documentación básica de los módulos.

## 10. Instrucción final

Antes de comenzar a programar, analiza los requerimientos y presenta la arquitectura propuesta, la estructura de carpetas, el diseño inicial de la base de datos y las dependencias necesarias.

Después comienza con la Etapa 1.

No generes código de relleno ni funcionalidades simuladas. Cada módulo debe quedar conectado a PostgreSQL y funcionar realmente.

Si encuentras una decisión técnica que no esté especificada, utiliza una solución profesional, documenta la decisión y continúa sin alterar las tecnologías obligatorias.

Prioriza la estabilidad, la integridad de los datos, la facilidad de mantenimiento y la posibilidad de ampliar el sistema en el futuro.
