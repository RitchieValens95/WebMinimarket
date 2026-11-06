-- Etapa 3: aplicar solo esta migración sobre la base ya validada.
BEGIN;
SET LOCAL search_path = pg_catalog, public;
SET LOCAL lock_timeout = '5s';
DO $migracion$
DECLARE
 bloque constant text := $contenido$
ALTER TABLE public.usuarios
 ADD COLUMN version_sesion integer NOT NULL DEFAULT 1 CHECK (version_sesion > 0),
 ADD COLUMN intentos_fallidos integer NOT NULL DEFAULT 0 CHECK (intentos_fallidos BETWEEN 0 AND 5),
 ADD COLUMN bloqueado_hasta timestamptz;
CREATE INDEX ix_usuarios_nombre_busqueda ON public.usuarios USING gin (nombre public.gin_trgm_ops);
$contenido$;
 calculado text := encode(sha256(convert_to(replace(bloque,chr(13)||chr(10),chr(10)),'UTF8')),'hex');
 registrado text;
BEGIN
 IF current_setting('server_version_num')::integer/10000 <> 18 OR current_database() IN ('postgres','template0','template1') THEN
  RAISE EXCEPTION 'Seleccione la base del sistema en PostgreSQL 18.';
 END IF;
 PERFORM pg_advisory_xact_lock(783144982201);
 IF to_regclass('public.esquema_versiones') IS NULL THEN RAISE EXCEPTION 'La base debe tener la etapa 2 validada.'; END IF;
 IF NOT EXISTS (SELECT 1 FROM public.esquema_versiones WHERE version='001' AND checksum='8c43de2fa290cfcf6680810d34479f5f09243004a045312d2e1b938e6a4bbf85') OR
    NOT EXISTS (SELECT 1 FROM public.esquema_versiones WHERE version='002' AND checksum='b5525cbca7af25a7c3ce6bc8aace77ef1e48e2e61fe18af837a7743f22c6a4e5') THEN
  RAISE EXCEPTION 'La base inicial no coincide con la versión validada.';
 END IF;
 SELECT checksum INTO registrado FROM public.esquema_versiones WHERE version='004';
 IF registrado IS NOT NULL THEN
  IF registrado<>calculado THEN RAISE EXCEPTION 'Checksum diferente para la migración 004.'; END IF;
  RAISE NOTICE 'Migración 004 ya aplicada.'; RETURN;
 END IF;
 EXECUTE bloque;
 INSERT INTO public.esquema_versiones(version,nombre,checksum) VALUES ('004','seguridad',calculado);
 RAISE NOTICE 'Migración 004 aplicada correctamente.';
END $migracion$;
COMMIT;
