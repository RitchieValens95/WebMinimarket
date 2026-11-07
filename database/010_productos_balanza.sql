-- Opciones de balanza: conservan datos, IVA y códigos anteriores.
BEGIN;
SET LOCAL search_path=pg_catalog,public;
SET LOCAL lock_timeout='5s';
DO $migracion$
DECLARE
 bloque constant text := $contenido$
ALTER TABLE public.productos
 ADD COLUMN balanza_pesable boolean NOT NULL DEFAULT false,
 ADD COLUMN balanza_unitario boolean NOT NULL DEFAULT false,
 ADD CONSTRAINT ck_productos_tipo_barra CHECK
 ((codigo_barras_generado::integer+balanza_pesable::integer+balanza_unitario::integer)<=1);
CREATE FUNCTION public.sv_codigo_balanza(producto bigint, unitario boolean) RETURNS text
 LANGUAGE plpgsql IMMUTABLE STRICT PARALLEL SAFE SET search_path=pg_catalog,public AS $fn$
BEGIN
 IF producto<1 OR producto>99999 THEN RAISE EXCEPTION 'El PLU debe tener de 1 a 5 dígitos, sin truncar el ID.' USING ERRCODE='23514'; END IF;
 RETURN (CASE WHEN unitario THEN '21' ELSE '20' END)||lpad(producto::text,5,'0');
END $fn$;
ALTER TABLE public.productos ADD COLUMN codigo_balanza text
 GENERATED ALWAYS AS (CASE WHEN balanza_pesable OR balanza_unitario THEN public.sv_codigo_balanza(id,balanza_unitario) ELSE NULL END) STORED;
ALTER TABLE public.productos ADD CONSTRAINT fk_producto_codigo_balanza
 FOREIGN KEY(id,codigo_balanza) REFERENCES public.productos_codigos_barras(producto_id,codigo) DEFERRABLE INITIALLY DEFERRED;
$contenido$;
 calculado text:=encode(sha256(convert_to(replace(bloque,chr(13)||chr(10),chr(10)),'UTF8')),'hex');
 registrado text;
BEGIN
 IF current_setting('server_version_num')::integer/10000<>18 OR current_database() IN ('postgres','template0','template1') THEN
  RAISE EXCEPTION 'Seleccione la base del sistema en PostgreSQL 18.';
 END IF;
 PERFORM pg_advisory_xact_lock(783144982201);
 IF to_regclass('public.esquema_versiones') IS NULL THEN RAISE EXCEPTION 'Falta la actualización 008 validada.'; END IF;
 IF NOT EXISTS(SELECT 1 FROM public.esquema_versiones WHERE version='008' AND checksum='9b0777ce8806d4c616acf110a2894aeddeefb692f62ce0f9ef316ee4534ae98f') THEN
  RAISE EXCEPTION 'La base no coincide con la actualización 008 validada.';
 END IF;
 SELECT checksum INTO registrado FROM public.esquema_versiones WHERE version='010';
 IF registrado IS NOT NULL THEN
  IF registrado<>calculado THEN RAISE EXCEPTION 'Checksum diferente para la migración 010.'; END IF;
  RAISE NOTICE 'Migración 010 ya aplicada.';RETURN;
 END IF;
 EXECUTE bloque;
 INSERT INTO public.esquema_versiones(version,nombre,checksum) VALUES('010','productos_balanza',calculado);
 RAISE NOTICE 'Migración 010 aplicada correctamente.';
END $migracion$;
COMMIT;
