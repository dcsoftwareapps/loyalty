param(
    [Parameter(Mandatory = $true)]
    [string]$SerialNumber,

    [string]$BaseUrl = "https://localhost:55128",

    [string]$OutputDir = ".\tmp"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$outputPath = Join-Path $OutputDir "$SerialNumber.pkpass"
$url = "$($BaseUrl.TrimEnd('/'))/api/dev/passes/$SerialNumber"

Invoke-WebRequest -Uri $url -OutFile $outputPath

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $outputPath))
try {
    Write-Host "Pass guardado: $outputPath"
    Write-Host "Archivos incluidos:"
    $zip.Entries |
        Sort-Object FullName |
        ForEach-Object { Write-Host ("- {0} ({1} bytes)" -f $_.FullName, $_.Length) }
}
finally {
    $zip.Dispose()
}
