-- SistemaVentas / etapa 2 original / datos_iniciales
-- Abrir y ejecutar el archivo completo en Query Tool, conectado a sistemaventas.
-- El checksum identifica el bloque de migración; no contiene credenciales.
BEGIN;
SET LOCAL search_path = pg_catalog, public;
SET LOCAL TIME ZONE 'America/Asuncion';
SET LOCAL lock_timeout = '5s';
SET LOCAL statement_timeout = '60s';
DO $migracion$
DECLARE
    bloque constant text := $contenido$
INSERT INTO public.roles(codigo, nombre) VALUES
 ('ADMINISTRADOR','Administrador'), ('SUPERVISOR','Supervisor'), ('CAJERO','Cajero');
INSERT INTO public.permisos(codigo, modulo, descripcion) VALUES
 ('USUARIOS_VER','USUARIOS','Consultar usuarios'),
 ('USUARIOS_ADMINISTRAR','USUARIOS','Registrar, editar y desactivar usuarios'),
 ('ROLES_ADMINISTRAR','USUARIOS','Administrar roles y permisos'),
 ('PRODUCTOS_VER','PRODUCTOS','Consultar productos'),
 ('PRODUCTOS_EDITAR','PRODUCTOS','Registrar, editar y desactivar productos'),
 ('CATEGORIAS_ADMINISTRAR','PRODUCTOS','Administrar categorías'),
 ('MARCAS_ADMINISTRAR','PRODUCTOS','Administrar marcas'),
 ('CLIENTES_VER','CLIENTES','Consultar clientes'),
 ('CLIENTES_REGISTRAR','CLIENTES','Registrar clientes'),
 ('CLIENTES_EDITAR','CLIENTES','Editar clientes'),
 ('PROVEEDORES_ADMINISTRAR','PROVEEDORES','Administrar proveedores'),
 ('VENTAS_CONFIRMAR','VENTAS','Confirmar ventas'),
 ('VENTAS_VER_PROPIAS','VENTAS','Consultar e imprimir ventas propias'),
 ('VENTAS_VER_TODAS','VENTAS','Consultar todas las ventas'),
 ('COMPRAS_ADMINISTRAR','COMPRAS','Registrar y consultar compras'),
 ('STOCK_VER','STOCK','Consultar existencias y movimientos'),
 ('STOCK_AJUSTAR','STOCK','Registrar ajustes de stock'),
 ('CAJA_ABRIR_PROPIA','CAJA','Abrir una sesión propia de caja'),
 ('CAJA_CERRAR_PROPIA','CAJA','Cerrar una sesión propia de caja'),
 ('CAJA_VER_PROPIA','CAJA','Consultar la caja propia'),
 ('CAJA_ADMINISTRAR','CAJA','Administrar cajas y sus sesiones'),
 ('CAJA_MOVIMIENTOS','CAJA','Registrar ingresos y egresos manuales'),
 ('DASHBOARD_VER','DASHBOARD','Consultar el panel principal'),
 ('REPORTES_VER','REPORTES','Consultar reportes globales'),
 ('REPORTES_EXPORTAR','REPORTES','Exportar reportes'),
 ('AUDITORIA_VER','AUDITORIA','Consultar auditoría');
INSERT INTO public.roles_permisos(rol_id, permiso_id)
 SELECT r.id, p.id FROM public.roles r CROSS JOIN public.permisos p
 WHERE r.codigo = 'ADMINISTRADOR';
INSERT INTO public.roles_permisos(rol_id, permiso_id)
 SELECT r.id, p.id FROM public.roles r CROSS JOIN public.permisos p
 WHERE r.codigo = 'SUPERVISOR' AND p.codigo NOT IN
 ('USUARIOS_VER','USUARIOS_ADMINISTRAR','ROLES_ADMINISTRAR');
INSERT INTO public.roles_permisos(rol_id, permiso_id)
 SELECT r.id, p.id FROM public.roles r CROSS JOIN public.permisos p
 WHERE r.codigo = 'CAJERO' AND p.codigo IN
 ('PRODUCTOS_VER','CLIENTES_VER','CLIENTES_REGISTRAR','VENTAS_CONFIRMAR',
  'VENTAS_VER_PROPIAS','CAJA_ABRIR_PROPIA','CAJA_CERRAR_PROPIA','CAJA_VER_PROPIA');
INSERT INTO public.unidades_medida(codigo, nombre) VALUES
 ('UN','Unidad'), ('KG','Kilogramo'), ('LT','Litro');
INSERT INTO public.metodos_pago(codigo, nombre, afecta_efectivo) VALUES
 ('EFECTIVO','Efectivo',true), ('TARJETA','Tarjeta',false),
 ('TRANSFERENCIA','Transferencia',false), ('OTROS','Otros',false);
INSERT INTO public.clientes(nombre, es_generico) VALUES ('Consumidor final',true);
INSERT INTO public.categorias(nombre) VALUES ('General');
INSERT INTO public.cajas(codigo, nombre) VALUES ('CAJA_01','Caja principal');
INSERT INTO public.configuracion_comercio(id, nombre, moneda, zona_horaria)
 VALUES (1,'Mi comercio','PYG','America/Asuncion');
-- Las cuentas de la aplicación se crearán con una contraseña local en la etapa 3.
$contenido$;
    calculado text := encode(sha256(convert_to(replace(bloque, chr(13) || chr(10), chr(10)), 'UTF8')), 'hex');
    registrado text;
    conflicto text;
BEGIN
    IF current_database() IN ('postgres','template0','template1') THEN
        RAISE EXCEPTION 'Seleccione la base del sistema, no una base administrativa o plantilla.';
    END IF;
    IF current_setting('server_version_num')::integer / 10000 <> 18 THEN
        RAISE EXCEPTION 'Esta versión del esquema requiere PostgreSQL 18.';
    END IF;
    IF current_setting('server_encoding') <> 'UTF8' THEN
        RAISE EXCEPTION 'La base de datos debe usar codificación UTF8.';
    END IF;
    PERFORM pg_advisory_xact_lock(783144982201);

    IF to_regclass('public.esquema_versiones') IS NULL THEN
        RAISE EXCEPTION 'Ejecute primero 001_estructura.sql.';
    END IF;
    SELECT checksum INTO registrado FROM public.esquema_versiones WHERE version = '001';
    IF registrado IS DISTINCT FROM '8c43de2fa290cfcf6680810d34479f5f09243004a045312d2e1b938e6a4bbf85' THEN
        RAISE EXCEPTION 'La estructura inicial no coincide con la versión requerida.';
    END IF;

    IF to_regclass('public.esquema_versiones') IS NOT NULL THEN
        SELECT checksum INTO registrado FROM public.esquema_versiones WHERE version = '002';
        IF registrado IS NOT NULL THEN
            IF registrado <> calculado THEN
                RAISE EXCEPTION 'Checksum diferente para migración 002. Cree una nueva migración; no edite una versión aplicada.';
            END IF;
            RAISE NOTICE 'Migración 002 ya aplicada; no se modificaron datos.';
            RETURN;
        END IF;
    END IF;

    EXECUTE bloque;
    INSERT INTO public.esquema_versiones(version, nombre, checksum)
      VALUES ('002', 'datos_iniciales', calculado);
    RAISE NOTICE 'Migración 002 aplicada correctamente.';
END $migracion$;
COMMIT;
