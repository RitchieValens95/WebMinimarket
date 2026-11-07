BEGIN TRANSACTION READ ONLY;
SET LOCAL search_path=pg_catalog,public;
DO $comprobar$
DECLARE faltantes text;
BEGIN
 IF NOT EXISTS(SELECT 1 FROM public.esquema_versiones WHERE version='008' AND checksum='9b0777ce8806d4c616acf110a2894aeddeefb692f62ce0f9ef316ee4534ae98f') THEN RAISE EXCEPTION 'Falta 008 o su checksum no coincide.'; END IF;
 SELECT string_agg(e.nombre,', ') INTO faltantes FROM (VALUES
 ('productos','ck_productos_iva','c'),('productos','fk_producto_ean13_generado','f'),
 ('productos_codigos_barras','uq_producto_una_barra','u'),('productos_codigos_barras','uq_producto_barra_id_codigo','u')
 ) e(tabla,nombre,tipo) WHERE NOT EXISTS(SELECT 1 FROM pg_constraint c WHERE c.conrelid=to_regclass('public.'||e.tabla)
 AND c.conname=e.nombre AND c.contype=e.tipo::"char" AND c.convalidated);
 IF faltantes IS NOT NULL THEN RAISE EXCEPTION 'Faltan restricciones: %',faltantes; END IF;
 IF (SELECT count(*) FROM pg_attribute WHERE attrelid='public.productos'::regclass AND NOT attisdropped
     AND attname IN ('porcentaje_iva','codigo_barras_generado','ean13_generado'))<>3 THEN RAISE EXCEPTION 'Faltan columnas de IVA/EAN-13.'; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_attribute WHERE attrelid='public.productos'::regclass AND attname='ean13_generado' AND attgenerated='s') THEN RAISE EXCEPTION 'EAN-13 debe ser una columna generada.'; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='public.productos'::regclass AND conname='fk_producto_ean13_generado' AND condeferrable AND condeferred) THEN RAISE EXCEPTION 'La relación EAN-13 debe comprobarse al confirmar la transacción.'; END IF;
 IF public.sv_ean13_producto(26)<>'2800000000264' OR public.sv_ean13_producto(36)<>'2800000000363' THEN RAISE EXCEPTION 'El cálculo EAN-13 no coincide.'; END IF;
 IF EXISTS(SELECT 1 FROM public.productos_codigos_barras GROUP BY producto_id HAVING count(*)>1) THEN RAISE EXCEPTION 'Hay más de una barra por producto.'; END IF;
 IF EXISTS(SELECT 1 FROM public.productos WHERE porcentaje_iva NOT IN (0,5,10)) THEN RAISE EXCEPTION 'Hay tasas de IVA no admitidas.'; END IF;
 IF EXISTS(SELECT 1 FROM public.productos p WHERE p.codigo_barras_generado AND NOT EXISTS(
   SELECT 1 FROM public.productos_codigos_barras b WHERE b.producto_id=p.id AND b.codigo=public.sv_ean13_producto(p.id))) THEN RAISE EXCEPTION 'La barra generada no coincide con su producto.'; END IF;
 RAISE NOTICE 'IVA y código de barras único/EAN-13 verificados correctamente.';
END $comprobar$;
SELECT 'OK' AS estado,count(*) FILTER(WHERE porcentaje_iva IS NULL) AS productos_con_iva_pendiente FROM public.productos;
COMMIT;
