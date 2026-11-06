#!/usr/bin/env python3
"""Pruebas reales en PostgreSQL 18 temporal. Requiere Python 3 y Docker local.

No conecta a la base del usuario. No publica puertos. Elimina su contenedor al terminar.
"""
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
import os
import re
import secrets
import subprocess
import tempfile
import time


ROOT = Path(__file__).resolve().parents[1]
ENV = os.environ.copy()
for selector in ("DOCKER_HOST", "DOCKER_CONTEXT", "DOCKER_TLS", "DOCKER_TLS_VERIFY", "DOCKER_CERT_PATH"):
    ENV.pop(selector, None)
DOCKER = ["docker", "--host=unix:///var/run/docker.sock"]
NAME = "sv-esquema-pruebas-" + secrets.token_hex(5)
checks = 0


def docker(*args, **kwargs):
    return subprocess.run(DOCKER + list(args), env=ENV, capture_output=True, text=True, **kwargs)


def sql(query, database="sistemaventas", user="sistemaventas_app", required=True):
    result = docker("exec", "-i", NAME, "psql", "-X", "-qAt", "-v", "ON_ERROR_STOP=1",
                    "-U", user, "-d", database, input=query)
    if required and result.returncode:
        raise RuntimeError(result.stderr)
    return result


def expect(condition, description):
    global checks
    if not condition:
        raise AssertionError(description)
    checks += 1
    print("OK:", description, flush=True)


def apply(name, database="sistemaventas", required=True):
    return sql((ROOT / name).read_text(encoding="utf-8-sig"), database, required=required)


def database(name):
    sql("CREATE DATABASE " + name + " OWNER sistemaventas_app;", "postgres", "postgres")


def parallel(query, database="sistemaventas"):
    with ThreadPoolExecutor(max_workers=2) as pool:
        return list(pool.map(lambda _: sql(query, database, required=False), range(2)))


def main():
    started = False
    with tempfile.TemporaryDirectory(prefix="sv-pg18-") as folder:
        path = Path(folder) / "postgres.env"
        path.write_text("POSTGRES_PASSWORD=" + secrets.token_hex(32) + "\n", encoding="utf-8")
        path.chmod(0o600)
        try:
            result = docker("run", "--rm", "-d", "--name", NAME, "--env-file", str(path), "postgres:18")
            if result.returncode:
                raise RuntimeError(result.stderr)
            started = True
            for _ in range(100):
                # El servidor temporal de initdb solo escucha por socket; esperar TCP
                # evita confundirlo con el servidor definitivo del contenedor.
                if docker("exec", NAME, "pg_isready", "-h", "127.0.0.1", "-U", "postgres").returncode == 0:
                    break
                time.sleep(0.25)
            else:
                raise RuntimeError("PostgreSQL no quedó disponible a tiempo")
            sql("CREATE ROLE sistemaventas_app LOGIN;", "postgres", "postgres")
            database("sistemaventas")
            version = sql("SELECT version();").stdout.strip()
            print(version, flush=True)
            expect(version.startswith("PostgreSQL 18."), "Servidor PostgreSQL 18 real")
            result = apply("001_estructura.sql", "postgres", required=False)
            expect(result.returncode != 0 and "base administrativa" in result.stderr, "Se rechaza instalar en la base administrativa postgres")
            # Simula el checkout de Git para Windows. Luego se repetirá con LF.
            for name in ("001_estructura.sql", "002_datos_iniciales.sql"):
                sql((ROOT / name).read_text(encoding="utf-8-sig").replace("\n", "\r\n"))
            expect(sql("SELECT count(*) FROM esquema_versiones;").stdout.strip() == "2", "Migraciones aplicadas con saltos CRLF de Windows")
            expect(sql("SELECT count(*) FROM pg_tables WHERE schemaname='public';").stdout.strip() == "25", "25 tablas creadas")
            expect(sql("SELECT rolsuper FROM pg_roles WHERE rolname=current_user;").stdout.strip() == "f", "Migraciones ejecutadas sin superusuario")
            initial = sql("SELECT (SELECT count(*) FROM roles),(SELECT count(*) FROM permisos),(SELECT count(*) FROM roles_permisos);").stdout
            apply("001_estructura.sql")
            apply("002_datos_iniciales.sql")
            expect(initial == sql("SELECT (SELECT count(*) FROM roles),(SELECT count(*) FROM permisos),(SELECT count(*) FROM roles_permisos);").stdout,
                   "Repetir migraciones no duplica catálogos")
            expect(sql("SELECT count(*) FROM usuarios;").stdout.strip() == "0", "No hay contraseñas ni administradores predeterminados")
            expect(sql("SELECT count(*) FROM roles_permisos rp JOIN roles r ON r.id=rp.rol_id WHERE r.codigo='ADMINISTRADOR';").stdout.strip() == "26", "Administrador recibe los 26 permisos iniciales")
            expect(sql("SELECT count(*) FROM roles_permisos rp JOIN roles r ON r.id=rp.rol_id JOIN permisos p ON p.id=rp.permiso_id WHERE r.codigo='CAJERO' AND p.codigo='USUARIOS_ADMINISTRAR';").stdout.strip() == "0", "Cajero no administra usuarios")
            apply("003_comprobar_estructura.sql")
            result = sql("BEGIN; ALTER TABLE productos DISABLE TRIGGER producto_saldo_coherente; " +
                         (ROOT / "003_comprobar_estructura.sql").read_text(encoding="utf-8-sig").replace("BEGIN TRANSACTION READ ONLY;", ""), required=False)
            expect(result.returncode != 0 and "trigger activo" in result.stderr, "El verificador detecta una protección deshabilitada")
            result = sql("BEGIN; DROP INDEX ix_productos_descripcion; " +
                         (ROOT / "003_comprobar_estructura.sql").read_text(encoding="utf-8-sig").replace("BEGIN TRANSACTION READ ONLY;", ""), required=False)
            expect(result.returncode != 0 and "Faltan índices" in result.stderr, "El verificador detecta un índice faltante")
            result = apply("tests/001_integridad.sql")
            amount = re.search(r"\n(\d+)\s*$", result.stdout)
            expect(amount is not None and int(amount.group(1)) >= 44, "Pruebas de integridad SQL completas")
            print("Comprobaciones SQL:", amount.group(1), flush=True)
            expect(sql("SELECT count(*) FROM usuarios;").stdout.strip() == "0", "Las pruebas SQL revierten sus filas")
            apply("demo/001_productos.sql")
            apply("demo/001_productos.sql")
            expect(sql("SELECT count(*) FROM productos;").stdout.strip() == "3", "Demo repetible: tres productos sin duplicación")
            expect(sql("SELECT count(*) FROM productos WHERE stock_actual<>0;").stdout.strip() == "0", "Demo no inventa existencias sin movimientos")
            sql("UPDATE configuracion_comercio SET nombre='Comercio personalizado'; UPDATE roles SET nombre='Nombre personalizado' WHERE codigo='CAJERO'; DELETE FROM roles_permisos WHERE rol_id=(SELECT id FROM roles WHERE codigo='CAJERO') AND permiso_id=(SELECT id FROM permisos WHERE codigo='VENTAS_CONFIRMAR');")
            apply("002_datos_iniciales.sql")
            expect(sql("SELECT nombre FROM configuracion_comercio;").stdout.strip() == "Comercio personalizado", "Semilla repetida conserva configuración editada")
            expect(sql("SELECT nombre FROM roles WHERE codigo='CAJERO';").stdout.strip() == "Nombre personalizado", "Semilla repetida conserva roles editados")
            expect(sql("SELECT count(*) FROM roles_permisos WHERE rol_id=(SELECT id FROM roles WHERE codigo='CAJERO') AND permiso_id=(SELECT id FROM permisos WHERE codigo='VENTAS_CONFIRMAR');").stdout.strip() == "0", "Semilla repetida no repone permisos retirados")
            for name, marker in [("001_estructura.sql", "CREATE TABLE public.usuarios"), ("002_datos_iniciales.sql", "INSERT INTO public.roles")]:
                changed = (ROOT / name).read_text(encoding="utf-8-sig").replace(marker, "-- cambio dentro del bloque\n" + marker, 1)
                result = sql(changed, required=False)
                expect(result.returncode != 0 and "Checksum diferente" in result.stderr, "Se rechaza alteración de migración " + name[:3])
            database("sv_conflicto")
            sql("CREATE TABLE productos(id integer PRIMARY KEY, dato text); INSERT INTO productos VALUES(1,'preservar');", "sv_conflicto")
            result = apply("001_estructura.sql", "sv_conflicto", required=False)
            expect(result.returncode != 0 and "Ya existen objetos" in result.stderr, "Detección de tablas previas no reconocidas")
            expect(sql("SELECT dato FROM productos WHERE id=1;", "sv_conflicto").stdout.strip() == "preservar", "Los datos previos permanecen intactos")
            expect(sql("SELECT to_regclass('public.esquema_versiones') IS NULL;", "sv_conflicto").stdout.strip() == "t", "Un conflicto no deja migración parcial")
            database("sv_sin_estructura")
            expect(apply("002_datos_iniciales.sql", "sv_sin_estructura", required=False).returncode != 0, "Se rechaza semilla sin estructura")
            database("sv_semilla_conflicto")
            apply("001_estructura.sql", "sv_semilla_conflicto")
            sql("INSERT INTO roles(codigo,nombre) VALUES ('CAJERO','Conservar');", "sv_semilla_conflicto")
            expect(apply("002_datos_iniciales.sql", "sv_semilla_conflicto", required=False).returncode != 0, "Conflicto de semilla revierte toda la migración")
            expect(sql("SELECT count(*) FROM roles;", "sv_semilla_conflicto").stdout.strip() == "1", "Una semilla fallida no deja filas parciales")
            database("sv_migracion_paralela")
            query = (ROOT / "001_estructura.sql").read_text(encoding="utf-8-sig")
            with ThreadPoolExecutor(max_workers=2) as pool:
                results = list(pool.map(lambda _: sql(query, "sv_migracion_paralela", required=False), range(2)))
            expect(all(r.returncode == 0 for r in results), "Dos migraciones simultáneas se serializan")
            expect(sql("SELECT count(*) FROM esquema_versiones;", "sv_migracion_paralela").stdout.strip() == "1", "Una sola versión tras ejecución simultánea")
            sql("INSERT INTO usuarios(nombre_usuario,nombre,password_hash) VALUES ('__concurrencia__','Prueba',repeat('x',80));")
            query = "BEGIN; INSERT INTO cajas_sesiones(caja_id,usuario_apertura_id,monto_inicial) SELECT c.id,u.id,0 FROM cajas c CROSS JOIN usuarios u WHERE c.codigo='CAJA_01' AND u.nombre_usuario='__concurrencia__'; SELECT pg_sleep(0.2); COMMIT;"
            results = parallel(query)
            expect(sorted(r.returncode == 0 for r in results) == [False, True], "Dos aperturas simultáneas: solo una se registra")
            query = "BEGIN; INSERT INTO ventas(clave_idempotencia,solicitud_hash,cliente_id,usuario_id,caja_sesion_id,subtotal,total) SELECT '00000000-0000-0000-0000-000000000099',repeat('c',64),cl.id,u.id,s.id,0,0 FROM clientes cl CROSS JOIN usuarios u CROSS JOIN cajas_sesiones s WHERE cl.es_generico AND u.nombre_usuario='__concurrencia__' AND s.fecha_cierre IS NULL; SELECT pg_sleep(0.2); COMMIT;"
            results = parallel(query)
            expect(sorted(r.returncode == 0 for r in results) == [False, True], "Dos solicitudes con la misma clave: solo una venta")
            sql("BEGIN; UPDATE productos SET stock_actual=1 WHERE codigo='DEMO-UN'; INSERT INTO movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario) SELECT p.id,u.id,'SALDO_INICIAL',1,0,1,7000 FROM productos p CROSS JOIN usuarios u WHERE p.codigo='DEMO-UN' AND u.nombre_usuario='__concurrencia__'; COMMIT;")
            query = "BEGIN; WITH cambio AS (UPDATE productos SET stock_actual=stock_actual-1 WHERE codigo='DEMO-UN' AND stock_actual>=1 RETURNING id,stock_actual) INSERT INTO movimientos_stock(producto_id,usuario_id,tipo,cantidad,saldo_anterior,saldo_posterior,costo_unitario,observacion) SELECT c.id,u.id,'AJUSTE_SALIDA',1,c.stock_actual+1,c.stock_actual,7000,'Prueba concurrente' FROM cambio c CROSS JOIN usuarios u WHERE u.nombre_usuario='__concurrencia__'; COMMIT;"
            expect(all(r.returncode == 0 for r in parallel(query)), "Patrón de descuento condicionado ejecutado concurrentemente")
            expect(sql("SELECT stock_actual FROM productos WHERE codigo='DEMO-UN';").stdout.strip() == "0.000", "El último artículo solo se descuenta una vez")
            apply("003_comprobar_estructura.sql")
            print("TOTAL:", checks, "comprobaciones del ejecutor, más", amount.group(1), "comprobaciones SQL.", flush=True)
        finally:
            if started:
                result = docker("rm", "-f", NAME)
                if result.returncode:
                    raise RuntimeError("No se pudo eliminar el contenedor temporal: " + NAME)


if __name__ == "__main__":
    main()
