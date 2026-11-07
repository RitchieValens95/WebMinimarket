#!/usr/bin/env python3
"""Pruebas aisladas para Linux: Roslyn + referencias net48 + Mono + PostgreSQL 18.
No usa ni modifica la base del usuario. NuGet debe estar restaurado en packages/.
"""
import argparse
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import tempfile
import time
import xml.etree.ElementTree as E
from concurrent.futures import ThreadPoolExecutor

ROOT = Path(__file__).resolve().parents[2]
NS = {"m": "http://schemas.microsoft.com/developer/msbuild/2003"}
ENV = os.environ.copy()
for key in ("DOCKER_HOST", "DOCKER_CONTEXT", "DOCKER_TLS", "DOCKER_TLS_VERIFY", "DOCKER_CERT_PATH"):
    ENV.pop(key, None)
DOCKER = ["docker", "--host=unix:///var/run/docker.sock"]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dotnet", required=True, help="Ejecutable dotnet del SDK usado para Roslyn")
    parser.add_argument("--referencias", required=True, type=Path, help="Directorio de referencias oficiales .NET Framework 4.8")
    parser.add_argument("--solo-vistas", action="store_true", help="Compilar y comprobar Razor sin crear una base temporal")
    parser.add_argument("--catalogos", action="store_true", help="Comprobar etapa 4.1 sobre una base temporal independiente")
    args = parser.parse_args()
    sdk = subprocess.check_output([args.dotnet, "--list-sdks"], text=True).splitlines()[-1]
    version, location = sdk.split(" ", 1)
    csc = Path(location.strip("[]")) / version / "Roslyn/bincore/csc.dll"
    if not csc.exists() or not (args.referencias / "System.Web.dll").exists():
        raise RuntimeError("Falta Roslyn o el paquete de referencias net48")
    name = "sv-seguridad-" + secrets.token_hex(5)
    started = False
    with tempfile.TemporaryDirectory(prefix="sv-seguridad-") as temp:
        work = Path(temp)
        runtime = work / "runtime"
        runtime.mkdir()

        def docker(*parts, **kwargs):
            return subprocess.run(DOCKER + list(parts), env=ENV, capture_output=True, text=True, **kwargs)

        def compile_csharp(sources, references, output, exe=False, language="7.3"):
            lines = ["-nologo", "-nostdlib+", "-langversion:" + language,
                     "-target:" + ("exe" if exe else "library"), "-out:" + str(output)]
            lines += ["-r:" + str(p) for p in references]
            lines += [str(p) for p in sources]
            response = work / (output.stem + ".rsp")
            response.write_text("\n".join('"' + p + '"' for p in lines), encoding="utf-8")
            subprocess.run([args.dotnet, str(csc), "@" + str(response)], check=True)

        web_references = []
        for suffix in ("Entidades", "Datos", "Negocio", "Web"):
            folder = ROOT / ("SistemaVentas." + suffix)
            tree = E.parse(folder / (folder.name + ".csproj"))
            if tree.find(".//m:TargetFrameworkVersion", NS).text != "v4.8":
                raise RuntimeError("Se cambió el framework obligatorio")
            references = [args.referencias / "mscorlib.dll"]
            for node in tree.findall(".//m:Reference", NS):
                hint = node.find("m:HintPath", NS)
                path = folder / hint.text.replace("\\", "/") if hint is not None else args.referencias / (node.attrib["Include"].split(",")[0] + ".dll")
                if not path.exists():
                    raise RuntimeError("Restaura NuGet: falta " + str(path))
                references.append(path)
                if hint is not None:
                    shutil.copy2(path, runtime / path.name)
            references += [runtime / (node.text + ".dll") for node in tree.findall(".//m:ProjectReference/m:Name", NS)]
            sources = [folder / node.attrib["Include"].replace("\\", "/") for node in tree.findall(".//m:Compile", NS)]
            compile_csharp(sources, references, runtime / (folder.name + ".dll"))
            if suffix == "Web":
                web_references = references + [runtime / "SistemaVentas.Web.dll"]
            print("Compilado", folder.name, flush=True)
        framework = [args.referencias / (p + ".dll") for p in ("mscorlib", "System", "System.Core", "System.Xml", "System.Data", "System.Web", "System.Configuration", "System.ComponentModel.DataAnnotations", "Microsoft.CSharp")]
        references = framework + list(runtime.glob("*.dll"))
        for source in ("SeguridadPruebas", "RazorPruebas"):
            compile_csharp([ROOT / "tests/seguridad" / (source + ".cs")], references, runtime / (source + ".exe"), exe=True)
        if args.catalogos:
            compile_csharp([ROOT / "tests/catalogos/CatalogosPruebas.cs"], references, runtime / "CatalogosPruebas.exe", exe=True)
        compile_csharp([ROOT / "tools/AdministradorInicial.cs"], references, runtime / "AdministradorInicial.exe", exe=True, language="5")
        # Solo algoritmos/redirecciones de la aplicación; no se copian secretos reales.
        web = (ROOT / "SistemaVentas.Web/Web.config").read_text(encoding="utf-8-sig")
        import re
        bindings = re.search(r"<runtime>.*?</runtime>", web, re.S).group()
        config = '<?xml version="1.0" encoding="utf-8"?><configuration>' + bindings + '</configuration>'
        for p in runtime.glob("*.exe"):
            (runtime / (p.name + ".config")).write_text(config, encoding="utf-8")
        result = docker("run", "--rm", "--network", "none", "--mount", "type=bind,src=" + str(runtime) + ",dst=/runtime",
                        "--mount", "type=bind,src=" + str(ROOT / "SistemaVentas.Web") + ",dst=/app,readonly",
                        "mono:6.12", "mono", "/runtime/RazorPruebas.exe")
        print(result.stdout, flush=True)
        if result.returncode:
            raise RuntimeError(result.stderr)
        compile_csharp(list(runtime.glob("Vista*.cs")), web_references, runtime / "VistasCompiladas.dll")
        print("OK: vistas Razor compiladas contra .NET Framework 4.8.", flush=True)
        if args.solo_vistas:
            return
        credential = secrets.token_hex(24)
        envfile = work / "postgres.env"
        envfile.write_text("POSTGRES_PASSWORD=" + secrets.token_hex(24) + "\nPOSTGRES_INITDB_ARGS=--auth-host=scram-sha-256\n")
        envfile.chmod(0o600)
        try:
            result = docker("run", "--rm", "-d", "--name", name, "--env-file", str(envfile), "postgres:18")
            if result.returncode:
                raise RuntimeError(result.stderr)
            started = True
            for _ in range(100):
                if docker("exec", name, "pg_isready", "-h", "127.0.0.1", "-U", "postgres").returncode == 0:
                    break
                time.sleep(0.25)
            else:
                raise RuntimeError("PostgreSQL no quedó listo")

            def sql(query, admin=False, required=True):
                result = docker("exec", "-i", name, "psql", "-X", "-qAt", "-v", "ON_ERROR_STOP=1", "-U",
                                "postgres" if admin else "sistemaventas_app", "-d", "postgres" if admin else "seguridad", input=query)
                if required and result.returncode:
                    raise RuntimeError(result.stderr)
                return result

            sql("CREATE ROLE sistemaventas_app LOGIN PASSWORD '" + credential + "'; CREATE DATABASE seguridad OWNER sistemaventas_app;", admin=True)
            # Únicamente en esta base temporal se crean las etapas anteriores como fixture.
            for filename in ("001_estructura.sql", "002_datos_iniciales.sql", "004_seguridad.sql", "004_seguridad.sql", "003_comprobar_estructura.sql", "005_comprobar_seguridad.sql"):
                result = sql((ROOT / "database" / filename).read_text(encoding="utf-8-sig"))
                print("OK SQL", filename, flush=True)
            changed = (ROOT / "database/004_seguridad.sql").read_text(encoding="utf-8-sig").replace("ALTER TABLE public.usuarios", "-- cambio de prueba\nALTER TABLE public.usuarios", 1)
            result = sql(changed, required=False)
            if result.returncode == 0 or "Checksum diferente" not in result.stderr:
                raise RuntimeError("No se detectó el cambio de checksum de 004")
            if args.catalogos:
                # Una base anterior puede contener relaciones hoy incompatibles.
                # La migración debe rechazarlas sin repararlas ni dejar DDL parcial.
                sql("INSERT INTO categorias(nombre,activo) VALUES ('Preflight inactiva',false); "
                    "INSERT INTO productos(codigo,descripcion,categoria_id,unidad_medida_id) "
                    "SELECT 'PREFLIGHT','Preflight',c.id,u.id FROM categorias c CROSS JOIN unidades_medida u "
                    "WHERE c.nombre='Preflight inactiva' AND u.codigo='UN';")
                migration = (ROOT / "database/006_catalogos.sql").read_text(encoding="utf-8-sig")
                result = sql(migration, required=False)
                if result.returncode == 0 or "Hay productos activos" not in result.stderr:
                    raise RuntimeError("006 no rechazó la relación previa incompatible")
                state = sql("SELECT (SELECT count(*) FROM esquema_versiones WHERE version='006'), "
                            "(SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND table_name='productos' AND column_name='requiere_catalogos_activos'), "
                            "(SELECT count(*) FROM productos WHERE codigo='PREFLIGHT');").stdout.strip()
                if state != "0|0|1":
                    raise RuntimeError("006 no revirtió completamente o modificó datos existentes")
                sql("DELETE FROM productos WHERE codigo='PREFLIGHT'; DELETE FROM categorias WHERE nombre='Preflight inactiva';")
                print("OK SQL: 006 rechaza relaciones incompatibles y revierte sin cambiar datos.", flush=True)
                for filename in ("006_catalogos.sql", "006_catalogos.sql", "007_comprobar_catalogos.sql", "003_comprobar_estructura.sql"):
                    sql((ROOT / "database" / filename).read_text(encoding="utf-8-sig"))
                    print("OK SQL", filename, flush=True)
                changed = (ROOT / "database/006_catalogos.sql").read_text(encoding="utf-8-sig").replace("-- Los productos", "-- cambio de prueba\n-- Los productos", 1)
                result = sql(changed, required=False)
                if result.returncode == 0 or "Checksum diferente" not in result.stderr:
                    raise RuntimeError("No se detectó el cambio de checksum de 006")
            connection = work / "cliente.env"
            connection.write_text("SV_TEST_CONNECTION=Host=127.0.0.1;Port=5432;Database=seguridad;Username=sistemaventas_app;Password=" + credential + "\n")
            connection.chmod(0o600)
            def mono(executable):
                result = docker("run", "--rm", "--network", "container:" + name,
                                "--env-file", str(connection), "--mount", "type=bind,src=" + str(runtime) + ",dst=/runtime",
                                "--mount", "type=bind,src=" + str(ROOT / "SistemaVentas.Web") + ",dst=/app,readonly",
                                "mono:6.12", "mono", "/runtime/" + executable)
                print(result.stdout, flush=True)
                if result.returncode:
                    raise RuntimeError(result.stderr)
            mono("CatalogosPruebas.exe" if args.catalogos else "SeguridadPruebas.exe")
            sql((ROOT / "database/005_comprobar_seguridad.sql").read_text(encoding="utf-8-sig"))
            if args.catalogos:
                sql((ROOT / "database/007_comprobar_catalogos.sql").read_text(encoding="utf-8-sig"))
                sql((ROOT / "database/003_comprobar_estructura.sql").read_text(encoding="utf-8-sig"))
        finally:
            if started:
                result = docker("rm", "-f", name)
                if result.returncode:
                    raise RuntimeError("No se pudo eliminar el contenedor temporal " + name)


if __name__ == "__main__":
    main()
