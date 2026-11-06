-- Solo en una base de pruebas: ROLLBACK revierte filas, pero no los avances de secuencias.
BEGIN;
SET LOCAL TIME ZONE 'America/Asuncion';
CREATE TEMP TABLE resultados(nombre text NOT NULL);
CREATE FUNCTION pg_temp.exigir_error(consulta text, codigo text, prueba text) RETURNS void LANGUAGE plpgsql AS $fn$
DECLARE obtenido text;
BEGIN
    BEGIN
        EXECUTE consulta;
        RAISE EXCEPTION 'No se rechazó: %', prueba USING ERRCODE = 'ZX001';
    EXCEPTION WHEN OTHERS THEN
        GET STACKED DIAGNOSTICS obtenido = RETURNED_SQLSTATE;
        IF obtenido <> codigo THEN RAISE; END IF;
    END;
    INSERT INTO resultados VALUES (prueba);
END $fn$;
DO $pruebas$
DECLARE u bigint; c bigint; m bigint; un bigint; kg bigint; p bigint; entero bigint;
        sesion bigint; venta bigint; detalle bigint; pago bigint; proveedor bigint;
        compra bigint; compra_detalle bigint; otro bigint;
BEGIN
    INSERT INTO public.usuarios(nombre_usuario,nombre,password_hash)
      VALUES ('__prueba__','Usuario temporal de pruebas',repeat('x',80)) RETURNING id INTO u;
    SELECT id INTO STRICT c FROM public.categorias WHERE nombre='General';
    SELECT id INTO STRICT un FROM public.unidades_medida WHERE codigo='UN';
    SELECT id INTO STRICT kg FROM public.unidades_medida WHERE codigo='KG';
    INSERT INTO public.productos(codigo,descripcion,categoria_id,unidad_medida_id,permite_decimales,precio_venta,precio_compra)
      VALUES ('__KG__','Producto temporal por peso',c,kg,true,10000,5000) RETURNING id INTO p;
    INSERT INTO public.productos(codigo,descripcion,categoria_id,unidad_medida_id)
      VALUES ('__UN__','Producto temporal entero',c,un) RETURNING id INTO entero;
    INSERT INTO public.productos_codigos_barras(producto_id,codigo) VALUES (p,'0000123456789');
    IF NOT EXISTS (SELECT 1 FROM public.productos_codigos_barras WHERE codigo='0000123456789') THEN RAISE EXCEPTION 'Se perdieron ceros iniciales'; END IF;
    INSERT INTO resultados VALUES ('Código de barras conserva ceros iniciales');
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.productos_codigos_barras(producto_id,codigo) VALUES (%s,''0000123456789'')',p),'23505','Código de barras duplicado');
    PERFORM pg_temp.exigir_error(format('UPDATE public.productos SET codigo=''__kg__'' WHERE id=%s',entero),'23505','Código interno sin distinguir mayúsculas');
    PERFORM pg_temp.exigir_error(format('UPDATE public.productos SET categoria_id=9223372036854775807 WHERE id=%s',p),'23503','Categoría inexistente');
    PERFORM pg_temp.exigir_error(format('UPDATE public.productos SET precio_venta=-1 WHERE id=%s',p),'23514','Precio negativo');
    PERFORM pg_temp.exigir_error(format('UPDATE public.productos SET stock_actual=-1 WHERE id=%s',p),'23514','Stock negativo');
    PERFORM pg_temp.exigir_error(format('UPDATE public.productos SET precio_venta=''NaN''::numeric WHERE id=%s',p),'23514','Importe NaN');
    PERFORM pg_temp.exigir_error('INSERT INTO public.clientes(nombre,es_generico) VALUES (''Otro genérico'',true)','23505','Un solo cliente genérico');
    INSERT INTO public.usuarios_roles SELECT u,id FROM public.roles WHERE codigo='CAJERO';
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.usuarios_roles SELECT %s,id FROM public.roles WHERE codigo=''CAJERO''',u),'23505','Asignación de rol duplicada');
    INSERT INTO public.cajas_sesiones(caja_id,usuario_apertura_id,monto_inicial)
      SELECT id,u,1000 FROM public.cajas WHERE codigo='CAJA_01' RETURNING id INTO sesion;
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.cajas_sesiones(caja_id,usuario_apertura_id,monto_inicial) SELECT id,%s,0 FROM public.cajas WHERE codigo=''CAJA_01''',u),'23505','Una sola apertura por caja');

    -- Saldo inicial real dentro de la misma transacción que su movimiento.
    UPDATE public.productos SET stock_actual=2.750 WHERE id=p;
    INSERT INTO public.movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario)
      VALUES (p,u,'SALDO_INICIAL',2.750,0,2.750,5000);
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    INSERT INTO resultados VALUES ('Cantidad decimal 2.750 y saldo inicial coherente');
    PERFORM pg_temp.exigir_error(format('UPDATE public.productos SET stock_actual=3 WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',p),'23514','Cambio de stock sin movimiento');
    PERFORM pg_temp.exigir_error(format('DELETE FROM public.movimientos_stock WHERE producto_id=%s',p),'23514','Historial de stock inmutable');

    INSERT INTO public.ventas(clave_idempotencia,solicitud_hash,cliente_id,usuario_id,caja_sesion_id,subtotal,total)
      SELECT '00000000-0000-0000-0000-000000000001',repeat('a',64),id,u,sesion,2500,2500 FROM public.clientes WHERE es_generico
      RETURNING id INTO venta;
    PERFORM pg_temp.exigir_error(format('UPDATE public.ventas SET estado=''CONFIRMADA'' WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',venta),'23514','Venta vacía no confirmable');
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.ventas(clave_idempotencia,solicitud_hash,cliente_id,usuario_id,caja_sesion_id,subtotal,total) SELECT clave_idempotencia,solicitud_hash,cliente_id,usuario_id,caja_sesion_id,subtotal,total FROM public.ventas WHERE id=%s',venta),'23505','Venta duplicada por idempotencia');
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.ventas_detalles(venta_id,producto_id,codigo_producto,descripcion_producto,unidad_medida,cantidad,precio_unitario,subtotal,costo_unitario) VALUES (%s,%s,''X'',''X'',''UN'',0.25,10000,2500,0)',venta,entero),'23514','Fracción en producto de unidades enteras');
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.ventas_detalles(venta_id,producto_id,codigo_producto,descripcion_producto,unidad_medida,cantidad,precio_unitario,subtotal,costo_unitario) VALUES (%s,%s,''X'',''X'',''KG'',0,10000,0,0)',venta,p),'23514','Cantidad cero');
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.ventas_detalles(venta_id,producto_id,codigo_producto,descripcion_producto,unidad_medida,cantidad,precio_unitario,descuento,subtotal,costo_unitario) VALUES (%s,%s,''X'',''X'',''KG'',1,100,101,-1,0)',venta,p),'23514','Descuento superior al importe bruto');
    INSERT INTO public.ventas_detalles(venta_id,producto_id,codigo_producto,descripcion_producto,unidad_medida,cantidad,precio_unitario,subtotal,costo_unitario)
      VALUES (venta,p,'__KG__','Producto temporal por peso','KG',0.250,10000,2500,5000) RETURNING id INTO detalle;
    PERFORM pg_temp.exigir_error(format('UPDATE public.ventas SET subtotal=2501,total=2501,estado=''CONFIRMADA'' WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',venta),'23514','Totales de cabecera inconsistentes');
    SELECT id INTO STRICT m FROM public.metodos_pago WHERE codigo='EFECTIVO';
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.pagos(venta_id,metodo_pago_id,monto,importe_recibido,vuelto) VALUES (%s,%s,2500,2000,-500)',venta,m),'23514','Efectivo recibido insuficiente');
    INSERT INTO public.pagos(venta_id,metodo_pago_id,monto,importe_recibido,vuelto)
      VALUES (venta,m,2500,3000,500) RETURNING id INTO pago;
    PERFORM pg_temp.exigir_error('SET CONSTRAINTS ALL IMMEDIATE','23514','Pago no puede quedar en una venta sin confirmar');
    PERFORM pg_temp.exigir_error(format('UPDATE public.ventas SET estado=''CONFIRMADA'' WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',venta),'23514','Venta sin salida de stock');
    UPDATE public.productos SET stock_actual=2.500 WHERE id=p;
    INSERT INTO public.movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,venta_detalle_id)
      VALUES (p,u,'SALIDA_VENTA',0.250,2.750,2.500,5000,detalle);
    PERFORM pg_temp.exigir_error('SET CONSTRAINTS ALL IMMEDIATE','23514','Salida comercial no puede quedar sin venta confirmada');
    PERFORM pg_temp.exigir_error(format('UPDATE public.ventas SET estado=''CONFIRMADA'' WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',venta),'23514','Venta sin ingreso en efectivo');
    INSERT INTO public.cajas_movimientos(caja_sesion_id,usuario_id,tipo,importe,concepto,pago_id)
      VALUES (sesion,u,'INGRESO',2500,'Venta temporal',pago);
    PERFORM pg_temp.exigir_error(format('UPDATE public.pagos SET monto=2499,vuelto=501 WHERE id=%s; UPDATE public.ventas SET estado=''CONFIRMADA'' WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',pago,venta),'23514','Pago diferente del total');
    PERFORM pg_temp.exigir_error(format('UPDATE public.ventas SET estado=''CONFIRMADA'' WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',venta),'23514','Confirmación requiere auditoría');
    INSERT INTO public.auditoria(usuario_id,accion,entidad,entidad_id) VALUES (u,'VENTA_CONFIRMADA','ventas',venta);
    PERFORM pg_temp.exigir_error(format('UPDATE public.pagos SET metodo_pago_id=(SELECT id FROM public.metodos_pago WHERE codigo=''TARJETA''),importe_recibido=NULL,vuelto=NULL WHERE id=%s; UPDATE public.ventas SET estado=''CONFIRMADA'' WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',pago,venta),'23514','Cambio de método no deja un ingreso ficticio en efectivo');
    PERFORM pg_temp.exigir_error(format('UPDATE public.pagos SET monto=2499,vuelto=501 WHERE id=%s; UPDATE public.ventas_detalles SET descuento=1,subtotal=2499 WHERE id=%s; UPDATE public.ventas SET descuento=1,total=2499,estado=''CONFIRMADA'' WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',pago,detalle,venta),'23514','Pago recalculado debe coincidir con el ingreso de caja');
    UPDATE public.ventas SET estado='CONFIRMADA' WHERE id=venta;
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    INSERT INTO resultados VALUES ('Venta coherente: detalle, pago, salida de stock, efectivo y auditoría');
    PERFORM pg_temp.exigir_error(format('UPDATE public.ventas SET total=0,subtotal=0 WHERE id=%s',venta),'23514','Venta confirmada inmutable');
    PERFORM pg_temp.exigir_error(format('DELETE FROM public.ventas_detalles WHERE id=%s',detalle),'23514','Detalle confirmado inmutable');
    PERFORM pg_temp.exigir_error(format('UPDATE public.pagos SET monto=1 WHERE id=%s',pago),'23514','Pago confirmado inmutable');
    PERFORM pg_temp.exigir_error(format('UPDATE public.productos SET stock_actual=2.250 WHERE id=%s; INSERT INTO public.movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,venta_detalle_id) VALUES (%s,%s,''SALIDA_VENTA'',0.250,2.500,2.250,5000,%s)',p,p,u,detalle),'23505','Un solo movimiento por detalle vendido');

    -- Entrada de compra: se conserva el costo del detalle y el saldo exacto.
    INSERT INTO public.proveedores(nombre) VALUES ('Proveedor temporal') RETURNING id INTO proveedor;
    INSERT INTO public.compras(proveedor_id,usuario_id,clave_idempotencia,solicitud_hash,total)
      VALUES (proveedor,u,'00000000-0000-0000-0000-000000000002',repeat('b',64),1500) RETURNING id INTO compra;
    INSERT INTO public.compras_detalles(compra_id,producto_id,cantidad,costo_unitario,subtotal)
      VALUES (compra,p,0.250,6000,1500) RETURNING id INTO compra_detalle;
    PERFORM pg_temp.exigir_error(format('UPDATE public.compras SET estado=''CONFIRMADA'' WHERE id=%s; SET CONSTRAINTS ALL IMMEDIATE',compra),'23514','Compra sin entrada de stock');
    UPDATE public.productos SET stock_actual=2.750,precio_compra=round((2.500*5000+0.250*6000)/2.750,4) WHERE id=p;
    INSERT INTO public.movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,compra_detalle_id)
      VALUES (p,u,'ENTRADA_COMPRA',0.250,2.500,2.750,6000,compra_detalle);
    UPDATE public.compras SET estado='CONFIRMADA' WHERE id=compra;
    INSERT INTO public.auditoria(usuario_id,accion,entidad,entidad_id) VALUES (u,'COMPRA_CONFIRMADA','compras',compra);
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    INSERT INTO resultados VALUES ('Compra coherente con entrada decimal y costo promedio');

    INSERT INTO public.ventas(clave_idempotencia,solicitud_hash,cliente_id,usuario_id,caja_sesion_id,subtotal,total)
      SELECT '00000000-0000-0000-0000-000000000004',repeat('d',64),id,u,sesion,0,0 FROM public.clientes WHERE es_generico RETURNING id INTO otro;
    INSERT INTO public.ventas_detalles(venta_id,producto_id,codigo_producto,descripcion_producto,unidad_medida,cantidad,precio_unitario,subtotal,costo_unitario)
      SELECT otro,id,codigo,descripcion,'KG',0.250,0,0,precio_compra FROM public.productos WHERE id=p RETURNING id INTO detalle;
    UPDATE public.productos SET stock_actual=2.500 WHERE id=p;
    INSERT INTO public.movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,venta_detalle_id)
      SELECT p,u,'SALIDA_VENTA',0.250,2.750,2.500,costo_unitario,detalle FROM public.ventas_detalles WHERE id=detalle;
    INSERT INTO public.auditoria(usuario_id,accion,entidad,entidad_id) VALUES (u,'VENTA_CONFIRMADA','ventas',otro);
    UPDATE public.ventas SET estado='CONFIRMADA' WHERE id=otro;
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    IF EXISTS (SELECT 1 FROM public.pagos WHERE venta_id=otro) THEN RAISE EXCEPTION 'Se inventó un pago para una venta de total cero'; END IF;
    INSERT INTO resultados VALUES ('Venta de total cero sin pagos ni ingresos ficticios');
    UPDATE public.productos SET stock_actual=4.000 WHERE id=p;
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario) VALUES (%s,%s,''AJUSTE_ENTRADA'',1.500,2.500,4.000,5000)',p,u),'23514','Ajuste requiere observación');
    INSERT INTO public.movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,observacion)
      VALUES (p,u,'AJUSTE_ENTRADA',1.500,2.500,4.000,5000,'Prueba de cantidad decimal');
    SET CONSTRAINTS ALL IMMEDIATE;
    SET CONSTRAINTS ALL DEFERRED;
    INSERT INTO resultados VALUES ('Ajuste decimal 1.500 conserva la trazabilidad');

    PERFORM pg_temp.exigir_error(format('UPDATE public.cajas_sesiones SET fecha_cierre=CURRENT_TIMESTAMP,usuario_cierre_id=%s,efectivo_esperado=9999,efectivo_contado=9999,diferencia=0 WHERE id=%s',u,sesion),'23514','Cierre con efectivo esperado incorrecto');
    UPDATE public.cajas_sesiones SET fecha_cierre=CURRENT_TIMESTAMP,usuario_cierre_id=u,efectivo_esperado=3500,efectivo_contado=3400,diferencia=-100 WHERE id=sesion;
    INSERT INTO resultados VALUES ('Cierre con diferencia negativa y efectivo neto de vuelto');
    PERFORM pg_temp.exigir_error(format('UPDATE public.cajas_sesiones SET fecha_cierre=NULL WHERE id=%s',sesion),'23514','Caja cerrada no puede reabrirse');
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.cajas_movimientos(caja_sesion_id,usuario_id,tipo,importe,concepto) VALUES (%s,%s,''INGRESO'',1,''Inválido'')',sesion,u),'23514','Movimiento en caja cerrada');
    PERFORM pg_temp.exigir_error(format('INSERT INTO public.ventas(clave_idempotencia,solicitud_hash,cliente_id,usuario_id,caja_sesion_id,subtotal,total) SELECT ''00000000-0000-0000-0000-000000000003'',solicitud_hash,cliente_id,usuario_id,caja_sesion_id,0,0 FROM public.ventas WHERE id=%s',venta),'23514','Venta en caja cerrada');
    PERFORM pg_temp.exigir_error('UPDATE public.auditoria SET accion=''MODIFICADA''','23514','Auditoría inmutable');
    SET CONSTRAINTS ALL IMMEDIATE;
END $pruebas$;
SELECT nombre AS prueba, 'OK' AS resultado FROM resultados ORDER BY nombre;
SELECT count(*) AS comprobaciones_correctas FROM resultados;
ROLLBACK;
