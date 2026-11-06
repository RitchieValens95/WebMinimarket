BEGIN TRANSACTION READ ONLY;
DO $comprobar$
BEGIN
 IF NOT EXISTS (SELECT 1 FROM public.esquema_versiones WHERE version='004' AND checksum='1f9563764c55ae59b97e60b8624833b94f473d563bdc1a912d6cdcd90d12f0fd') THEN
  RAISE EXCEPTION 'Falta aplicar 004_seguridad.sql o su checksum no coincide.';
 END IF;
 IF (SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='usuarios'
     AND column_name IN ('version_sesion','intentos_fallidos','bloqueado_hasta'))<>3 THEN
  RAISE EXCEPTION 'Faltan columnas de seguridad.';
 END IF;
 IF to_regclass('public.ix_usuarios_nombre_busqueda') IS NULL THEN RAISE EXCEPTION 'Falta el índice de búsqueda de usuarios.'; END IF;
 RAISE NOTICE 'Estructura de seguridad verificada correctamente.';
END $comprobar$;
SELECT 'OK' AS estado, (SELECT count(*) FROM public.usuarios) AS usuarios,
 (SELECT count(*) FROM public.roles WHERE activo) AS roles_activos,
 (SELECT count(*) FROM public.permisos) AS permisos;
COMMIT;
