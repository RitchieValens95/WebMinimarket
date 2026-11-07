BEGIN TRANSACTION READ ONLY;
SET LOCAL search_path = pg_catalog, public;
DO $comprobar$
DECLARE faltantes text;
BEGIN
 IF NOT EXISTS (SELECT 1 FROM public.esquema_versiones WHERE version='006' AND checksum='17c3b54bb8c7fe236d128b7b0e0572b45bfd3119d31ebd6c8937b1be4cfb16d8') THEN
  RAISE EXCEPTION 'Falta aplicar 006_catalogos.sql o su checksum no coincide.';
 END IF;
 SELECT string_agg(e.nombre, ', ') INTO faltantes FROM (VALUES
  ('categorias','uq_categorias_id_activo','UNIQUE (id, activo)'),
  ('marcas','uq_marcas_id_activo','UNIQUE (id, activo)'),
  ('productos','fk_productos_categoria_activa','FOREIGN KEY (categoria_id, requiere_catalogos_activos) REFERENCES categorias(id, activo)'),
  ('productos','fk_productos_marca_activa','FOREIGN KEY (marca_id, requiere_catalogos_activos) REFERENCES marcas(id, activo)')
 ) e(tabla,nombre,definicion)
 WHERE NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid=to_regclass('public.'||e.tabla)
  AND c.conname=e.nombre AND c.convalidated AND NOT c.condeferrable
  AND pg_get_constraintdef(c.oid)=e.definicion);
 IF faltantes IS NOT NULL THEN RAISE EXCEPTION 'Restricciones de catálogo ausentes o diferentes: %',faltantes; END IF;
 IF NOT EXISTS (SELECT 1 FROM pg_attribute a JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
  WHERE a.attrelid='public.productos'::regclass AND a.attname='requiere_catalogos_activos'
  AND a.atttypid='boolean'::regtype AND a.attgenerated='s'
  AND regexp_replace(pg_get_expr(d.adbin,d.adrelid),'[[:space:]()]','','g')='CASEWHENactivoTHENtrueELSENULL::booleanEND') THEN
  RAISE EXCEPTION 'Falta la columna generada que protege los catálogos activos.';
 END IF;
 IF EXISTS (SELECT 1 FROM public.productos p JOIN public.categorias c ON c.id=p.categoria_id
  LEFT JOIN public.marcas m ON m.id=p.marca_id WHERE p.activo AND (NOT c.activo OR (p.marca_id IS NOT NULL AND NOT m.activo))) THEN
  RAISE EXCEPTION 'Hay productos activos asociados a catálogos inactivos.';
 END IF;
 RAISE NOTICE 'Categorías, marcas y relaciones de productos verificadas correctamente.';
END $comprobar$;
SELECT 'OK' AS estado, (SELECT count(*) FROM public.categorias) AS categorias,
 (SELECT count(*) FROM public.marcas) AS marcas;
COMMIT;
