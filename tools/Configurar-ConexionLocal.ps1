param(
    [string]$Servidor = 'localhost',
    [int]$Puerto = 5432,
    [string]$BaseDatos = 'sistemaventas',
    [string]$Usuario = 'sistemaventas_app',
    [switch]$Sobrescribir
)

$ErrorActionPreference = 'Stop'
$archivo = Join-Path $PSScriptRoot '..\SistemaVentas.Web\AppSettings.Local.config'

if ((Test-Path $archivo) -and -not $Sobrescribir) {
    throw 'Ya existe la configuración local. Se conservó sin cambios. Usa -Sobrescribir solo si deseas reemplazarla.'
}

Add-Type -AssemblyName System.Data
$password = Read-Host "Contraseña de $Usuario (no la de postgres)" -AsSecureString
$puntero = [IntPtr]::Zero

try {
    $puntero = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($password)
    $cadena = New-Object System.Data.Common.DbConnectionStringBuilder
    $cadena['Host'] = $Servidor
    $cadena['Port'] = $Puerto
    $cadena['Database'] = $BaseDatos
    $cadena['Username'] = $Usuario
    $cadena['Password'] = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($puntero)
    $cadena['Timeout'] = 5
    $cadena['Command Timeout'] = 10
    $cadena['Application Name'] = 'SistemaVentas'

    # Los generadores escapan correctamente tanto la cadena como el XML.
    $xml = New-Object System.Xml.XmlDocument
    $declaracion = $xml.CreateXmlDeclaration('1.0', 'utf-8', $null)
    [void]$xml.AppendChild($declaracion)
    $raiz = $xml.CreateElement('appSettings')
    [void]$xml.AppendChild($raiz)
    $entrada = $xml.CreateElement('add')
    $entrada.SetAttribute('key', 'SistemaVentas.PostgreSql')
    $entrada.SetAttribute('value', $cadena.ConnectionString)
    [void]$raiz.AppendChild($entrada)
    $xml.Save($archivo)
    Write-Host 'Configuración local guardada. Su contenido es privado y está excluido de Git.'
}
finally {
    if ($puntero -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($puntero)
    }
    if ($null -ne $cadena) { $cadena.Clear() }
    if ($null -ne $xml) { $xml.RemoveAll() }
    $password.Dispose()
}
