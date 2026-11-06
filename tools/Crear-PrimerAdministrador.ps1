# Ejecutar en Windows PowerShell 5.1, después de compilar en Visual Studio.
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') {
    throw 'Usa Windows PowerShell 5.1: esta herramienta ejecuta .NET Framework 4.8.'
}
$raiz = Split-Path $PSScriptRoot -Parent
$binWeb = Join-Path $raiz 'SistemaVentas.Web\bin'
$salida = Join-Path $PSScriptRoot 'bin\administrador-inicial'
$config = Join-Path $raiz 'SistemaVentas.Web\Web.config'
$source = Join-Path $PSScriptRoot 'AdministradorInicial.cs'
foreach ($nombre in @('SistemaVentas.Entidades.dll', 'SistemaVentas.Datos.dll', 'SistemaVentas.Negocio.dll', 'Npgsql.dll')) {
    if (-not (Test-Path (Join-Path $binWeb $nombre))) { throw 'Primero restaura NuGet y compila toda la solución.' }
}
New-Item -ItemType Directory -Force -Path $salida | Out-Null
Copy-Item (Join-Path $binWeb '*.dll') $salida -Force
$ejecutable = Join-Path $salida 'CrearAdministrador.exe'
$opciones = New-Object System.CodeDom.Compiler.CompilerParameters
$opciones.GenerateExecutable = $true
$opciones.OutputAssembly = $ejecutable
$opciones.CompilerOptions = '/langversion:5'
foreach ($referencia in @('System.dll', 'System.Core.dll', 'System.Xml.dll')) { [void]$opciones.ReferencedAssemblies.Add($referencia) }
foreach ($nombre in @('SistemaVentas.Entidades.dll','SistemaVentas.Datos.dll','SistemaVentas.Negocio.dll')) {
    [void]$opciones.ReferencedAssemblies.Add((Join-Path $binWeb $nombre))
}
$compilador = New-Object Microsoft.CSharp.CSharpCodeProvider
try { $resultado = $compilador.CompileAssemblyFromFile($opciones, $source) }
finally { $compilador.Dispose() }
if ($resultado.Errors.HasErrors) { throw 'No se pudo compilar la herramienta. Revisa la compilación de la solución y .NET Framework 4.8.' }
# Se copian las redirecciones de ensamblados, nunca los valores de conexión.
[xml]$web = Get-Content -LiteralPath $config -Raw
$xml = New-Object System.Xml.XmlDocument
[void]$xml.AppendChild($xml.CreateXmlDeclaration('1.0','utf-8',$null))
$root = $xml.CreateElement('configuration')
[void]$xml.AppendChild($root)
[void]$root.AppendChild($xml.ImportNode($web.configuration.runtime,$true))
$xml.Save($ejecutable + '.config')
& $ejecutable $config
if ($LASTEXITCODE -ne 0) { throw 'La creación no terminó correctamente. Revisa el mensaje anterior.' }
