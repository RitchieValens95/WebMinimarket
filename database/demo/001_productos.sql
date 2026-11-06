-- Opcional. Productos identificados como DEMO, con stock cero y sin cuentas de acceso.
BEGIN;
SET LOCAL TIME ZONE 'America/Asuncion';
DO $demo$
BEGIN
    PERFORM pg_advisory_xact_lock(783144982201);
    IF NOT EXISTS (SELECT 1 FROM public.esquema_versiones WHERE version='002') THEN
        RAISE EXCEPTION 'Aplique primero los scripts 001 y 002.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM public.categorias WHERE lower(btrim(nombre))='general') OR
       (SELECT count(*) FROM public.unidades_medida WHERE codigo IN ('UN','KG')) <> 2 THEN
        RAISE EXCEPTION 'La demostración necesita los catálogos iniciales General, UN y KG.';
    END IF;
    INSERT INTO public.productos(codigo,descripcion,categoria_id,unidad_medida_id,permite_decimales,precio_compra,precio_venta,stock_minimo)
      SELECT d.codigo,d.descripcion,c.id,u.id,d.decimales,d.compra,d.venta,d.minimo
      FROM (VALUES
        ('DEMO-UN','Producto de ejemplo por unidad','UN',false,7000,10000,5),
        ('DEMO-KG','Producto de ejemplo por peso','KG',true,8000,12000,0.250),
        ('DEMO-FRAC','Producto de ejemplo fraccionable','UN',true,1500,2500,1.500)
      ) d(codigo,descripcion,unidad,decimales,compra,venta,minimo)
      JOIN public.categorias c ON lower(btrim(c.nombre))='general'
      JOIN public.unidades_medida u ON u.codigo=d.unidad
      ON CONFLICT (lower(btrim(codigo))) DO NOTHING;
    INSERT INTO public.productos_codigos_barras(producto_id,codigo)
      SELECT p.id,replace(p.codigo,'DEMO-','DEMO000') FROM public.productos p
      WHERE p.codigo IN ('DEMO-UN','DEMO-KG','DEMO-FRAC') ON CONFLICT (codigo) DO NOTHING;
END $demo$;
COMMIT;
