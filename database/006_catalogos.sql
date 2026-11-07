-- Etapa 4.1: aplicar sobre la base existente con la etapa 3 validada.
BEGIN;
SET LOCAL search_path = pg_catalog, public;
SET LOCAL lock_timeout = '5s';
DO $migracion$
DECLARE
 bloque constant text := $contenido$
-- Los productos inactivos conservan sus relaciones e historial.
-- Las FK compuestas también protegen frente a escrituras concurrentes.
DO $validar$
BEGIN
 IF EXISTS (SELECT 1 FROM public.productos p
     JOIN public.categorias c ON c.id=p.categoria_id
     LEFT JOIN public.marcas m ON m.id=p.marca_id
     WHERE p.activo AND (NOT c.activo OR (p.marca_id IS NOT NULL AND NOT m.activo))) THEN
  RAISE EXCEPTION 'Hay productos activos con categoría o marca inactiva. Revise esos registros antes de aplicar 006.';
 END IF;
END $validar$;
ALTER TABLE public.categorias ADD CONSTRAINT uq_categorias_id_activo UNIQUE (id,activo);
ALTER TABLE public.marcas ADD CONSTRAINT uq_marcas_id_activo UNIQUE (id,activo);
ALTER TABLE public.productos ADD COLUMN requiere_catalogos_activos boolean
 GENERATED ALWAYS AS (CASE WHEN activo THEN true ELSE NULL END) STORED;
ALTER TABLE public.productos ADD CONSTRAINT fk_productos_categoria_activa
 FOREIGN KEY (categoria_id,requiere_catalogos_activos) REFERENCES public.categorias(id,activo);
ALTER TABLE public.productos ADD CONSTRAINT fk_productos_marca_activa
 FOREIGN KEY (marca_id,requiere_catalogos_activos) REFERENCES public.marcas(id,activo);
$contenido$;
 calculado text := encode(sha256(convert_to(replace(bloque,chr(13)||chr(10),chr(10)),'UTF8')),'hex');
 registrado text;
BEGIN
 IF current_setting('server_version_num')::integer/10000 <> 18 OR current_database() IN ('postgres','template0','template1') THEN
  RAISE EXCEPTION 'Seleccione la base del sistema en PostgreSQL 18.';
 END IF;
 PERFORM pg_advisory_xact_lock(783144982201);
 IF to_regclass('public.esquema_versiones') IS NULL THEN RAISE EXCEPTION 'La base debe tener la etapa 3 validada.'; END IF;
 IF NOT EXISTS (SELECT 1 FROM public.esquema_versiones WHERE version='004' AND checksum='1f9563764c55ae59b97e60b8624833b94f473d563bdc1a912d6cdcd90d12f0fd') THEN
  RAISE EXCEPTION 'La base no coincide con la etapa 3 validada.';
 END IF;
 SELECT checksum INTO registrado FROM public.esquema_versiones WHERE version='006';
 IF registrado IS NOT NULL THEN
  IF registrado<>calculado THEN RAISE EXCEPTION 'Checksum diferente para la migración 006.'; END IF;
  RAISE NOTICE 'Migración 006 ya aplicada.'; RETURN;
 END IF;
 EXECUTE bloque;
 INSERT INTO public.esquema_versiones(version,nombre,checksum) VALUES ('006','catalogos',calculado);
 RAISE NOTICE 'Migración 006 aplicada correctamente.';
END $migracion$;
COMMIT;
