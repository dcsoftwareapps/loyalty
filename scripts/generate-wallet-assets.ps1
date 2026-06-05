param(
    [string]$SourceLogo = "C:\Users\chave\OneDrive\K-Beauty\Pass\logo.png",
    [string]$OutputDir = ".\src\KBeauty.Loyalty.Infrastructure\Assets\AppleWallet"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $SourceLogo)) {
    throw "Logo maestro no existe. Ruta esperada: $SourceLogo"
}

$resolvedOutput = Resolve-Path -LiteralPath "." | Select-Object -ExpandProperty Path
$targetDir = Join-Path $resolvedOutput $OutputDir
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

Add-Type -AssemblyName System.Drawing

function New-WalletAsset {
    param(
        [System.Drawing.Image]$Source,
        [string]$Name,
        [int]$Width,
        [int]$Height
    )

    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(72, 72)

    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality

    $scale = [Math]::Min($Width / $Source.Width, $Height / $Source.Height)
    $drawWidth = [Math]::Max(1, [int][Math]::Round($Source.Width * $scale))
    $drawHeight = [Math]::Max(1, [int][Math]::Round($Source.Height * $scale))
    $x = [int][Math]::Round(($Width - $drawWidth) / 2)
    $y = [int][Math]::Round(($Height - $drawHeight) / 2)

    $graphics.DrawImage($Source, $x, $y, $drawWidth, $drawHeight)

    $path = Join-Path $targetDir $Name
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)

    $graphics.Dispose()
    $bitmap.Dispose()

    Write-Host "$Name -> ${Width}x${Height} ($path)"
}

$sourceImage = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $SourceLogo))
try {
    New-WalletAsset -Source $sourceImage -Name "icon.png" -Width 29 -Height 29
    New-WalletAsset -Source $sourceImage -Name "icon@2x.png" -Width 58 -Height 58
    New-WalletAsset -Source $sourceImage -Name "icon@3x.png" -Width 87 -Height 87
    New-WalletAsset -Source $sourceImage -Name "logo.png" -Width 160 -Height 50
    New-WalletAsset -Source $sourceImage -Name "logo@2x.png" -Width 320 -Height 100
}
finally {
    $sourceImage.Dispose()
}
