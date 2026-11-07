BEGIN TRANSACTION READ ONLY;
SET LOCAL search_path=pg_catalog,public;
DO $comprobar$
BEGIN
 IF NOT EXISTS(SELECT 1 FROM public.esquema_versiones WHERE version='010' AND checksum='6acd2716874c025481b8486de1bd2fc51acf6bb6696dd5399cdabe35aa3b3c18') THEN RAISE EXCEPTION 'Falta 010 o su checksum no coincide.'; END IF;
 IF (SELECT count(*) FROM pg_attribute WHERE attrelid='public.productos'::regclass AND NOT attisdropped
  AND attname IN ('balanza_pesable','balanza_unitario','codigo_balanza'))<>3 THEN RAISE EXCEPTION 'Faltan columnas de balanza.'; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_attribute WHERE attrelid='public.productos'::regclass AND attname='codigo_balanza' AND attgenerated='s') THEN RAISE EXCEPTION 'El código de balanza debe ser generado.'; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='public.productos'::regclass AND conname='ck_productos_tipo_barra' AND contype='c' AND convalidated) THEN RAISE EXCEPTION 'Falta la restricción de tipo de barra.'; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='public.productos'::regclass AND conname='fk_producto_codigo_balanza' AND contype='f' AND convalidated AND condeferrable AND condeferred) THEN RAISE EXCEPTION 'Falta la relación diferida de balanza.'; END IF;
 IF public.sv_codigo_balanza(3,false)<>'2000003' OR public.sv_codigo_balanza(5,true)<>'2100005' THEN RAISE EXCEPTION 'Los ejemplos de balanza no coinciden.'; END IF;
 IF EXISTS(SELECT 1 FROM public.productos WHERE codigo_barras_generado::integer+balanza_pesable::integer+balanza_unitario::integer>1) THEN RAISE EXCEPTION 'Hay tipos de barra incompatibles.'; END IF;
 IF EXISTS(SELECT 1 FROM public.productos p WHERE (p.balanza_pesable OR p.balanza_unitario) AND NOT EXISTS(
  SELECT 1 FROM public.productos_codigos_barras b WHERE b.producto_id=p.id AND b.codigo=public.sv_codigo_balanza(p.id,p.balanza_unitario))) THEN RAISE EXCEPTION 'La identificación de balanza no coincide con su producto.'; END IF;
 RAISE NOTICE 'Productos de balanza verificados correctamente.';
END $comprobar$;
SELECT 'OK' AS estado,count(*) FILTER(WHERE balanza_pesable) AS pesables,count(*) FILTER(WHERE balanza_unitario) AS unitarios FROM public.productos;
COMMIT;
