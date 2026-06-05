param(
    [Parameter(Mandatory = $true)]
    [string]$PkpassPath,

    [string]$WorkDir = ".\tmp\pkpass-verify"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PkpassPath)) {
    throw "No existe el .pkpass: $PkpassPath"
}

if (Test-Path -LiteralPath $WorkDir) {
    Remove-Item -LiteralPath $WorkDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path -LiteralPath $PkpassPath), (Resolve-Path -LiteralPath $WorkDir))

$signaturePath = Join-Path $WorkDir "signature"
$manifestPath = Join-Path $WorkDir "manifest.json"

if (-not (Test-Path -LiteralPath $signaturePath)) {
    throw "El .pkpass no contiene archivo signature."
}

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "El .pkpass no contiene archivo manifest.json."
}

Write-Host "Archivos incluidos en el .pkpass:"
Get-ChildItem -LiteralPath $WorkDir | Sort-Object Name | ForEach-Object {
    Write-Host ("- {0} ({1} bytes)" -f $_.Name, $_.Length)
}

Write-Host ""
Write-Host "Validando firma PKCS#7 detached con OpenSSL..."
openssl smime -verify -inform DER -in $signaturePath -content $manifestPath -noverify
