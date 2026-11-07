-- Actualización de productos: IVA y una sola barra por producto.
BEGIN;
SET LOCAL search_path=pg_catalog,public;
SET LOCAL lock_timeout='5s';
DO $migracion$
DECLARE
 bloque constant text := $contenido$
DO $validar$
DECLARE repetidos text;
BEGIN
 SELECT string_agg(producto_id::text, ', ') INTO repetidos FROM
  (SELECT producto_id FROM public.productos_codigos_barras GROUP BY producto_id HAVING count(*)>1 ORDER BY producto_id LIMIT 20) x;
 IF repetidos IS NOT NULL THEN
  RAISE EXCEPTION 'Hay productos con más de un código de barras (ids: %). Elija cuál conservar antes de aplicar 008. No se borraron datos.',repetidos;
 END IF;
END $validar$;
-- El IVA anterior no se adivina: NULL significa pendiente de revisión.
ALTER TABLE public.productos ADD COLUMN porcentaje_iva smallint
 CONSTRAINT ck_productos_iva CHECK (porcentaje_iva IN (0,5,10));
ALTER TABLE public.productos ADD COLUMN codigo_barras_generado boolean NOT NULL DEFAULT false;
ALTER TABLE public.productos_codigos_barras ADD CONSTRAINT uq_producto_una_barra UNIQUE(producto_id);
ALTER TABLE public.productos_codigos_barras ADD CONSTRAINT uq_producto_barra_id_codigo UNIQUE(producto_id,codigo);
CREATE FUNCTION public.sv_ean13_producto(producto bigint) RETURNS text
 LANGUAGE plpgsql IMMUTABLE STRICT PARALLEL SAFE SET search_path=pg_catalog,public AS $fn$
DECLARE base text; suma integer:=0; i integer;
BEGIN
 IF producto<1 OR producto>9999999999 THEN RAISE EXCEPTION 'ID fuera del rango EAN-13 interno.' USING ERRCODE='23514'; END IF;
 base:='28'||lpad(producto::text,10,'0');
 FOR i IN 1..12 LOOP suma:=suma+substring(base FROM i FOR 1)::integer*(CASE WHEN i%2=1 THEN 1 ELSE 3 END); END LOOP;
 RETURN base||((10-suma%10)%10)::text;
END $fn$;
ALTER TABLE public.productos ADD COLUMN ean13_generado text
 GENERATED ALWAYS AS (CASE WHEN codigo_barras_generado THEN public.sv_ean13_producto(id) ELSE NULL END) STORED;
-- La fila de producto se inserta antes de su barra; se comprueba al COMMIT.
ALTER TABLE public.productos ADD CONSTRAINT fk_producto_ean13_generado
 FOREIGN KEY(id,ean13_generado) REFERENCES public.productos_codigos_barras(producto_id,codigo) DEFERRABLE INITIALLY DEFERRED;
$contenido$;
 calculado text:=encode(sha256(convert_to(replace(bloque,chr(13)||chr(10),chr(10)),'UTF8')),'hex');
 registrado text;
BEGIN
 IF current_setting('server_version_num')::integer/10000<>18 OR current_database() IN ('postgres','template0','template1') THEN
  RAISE EXCEPTION 'Seleccione la base del sistema en PostgreSQL 18.';
 END IF;
 PERFORM pg_advisory_xact_lock(783144982201);
 IF to_regclass('public.esquema_versiones') IS NULL THEN RAISE EXCEPTION 'Falta la etapa 4.1 validada.'; END IF;
 IF NOT EXISTS(SELECT 1 FROM public.esquema_versiones WHERE version='006' AND checksum='17c3b54bb8c7fe236d128b7b0e0572b45bfd3119d31ebd6c8937b1be4cfb16d8') THEN
  RAISE EXCEPTION 'La base no coincide con la etapa 4.1 validada.';
 END IF;
 SELECT checksum INTO registrado FROM public.esquema_versiones WHERE version='008';
 IF registrado IS NOT NULL THEN
  IF registrado<>calculado THEN RAISE EXCEPTION 'Checksum diferente para la migración 008.'; END IF;
  RAISE NOTICE 'Migración 008 ya aplicada.';RETURN;
 END IF;
 EXECUTE bloque;
 INSERT INTO public.esquema_versiones(version,nombre,checksum) VALUES('008','producto_iva_ean13',calculado);
 RAISE NOTICE 'Migración 008 aplicada correctamente.';
END $migracion$;
COMMIT;
