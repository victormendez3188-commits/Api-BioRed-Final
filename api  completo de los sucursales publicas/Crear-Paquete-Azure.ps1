param()

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot "BioRed\BioRed.csproj"
$publishDirectory = Join-Path $projectRoot "publish\azure"
$deployZip = Join-Path $projectRoot "BioRed-Azure-Deploy-v31.zip"

if (-not (Test-Path -LiteralPath $projectFile)) {
    throw "No se encontró el proyecto BioRed.csproj."
}

if (Test-Path -LiteralPath $publishDirectory) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $deployZip) {
    Remove-Item -LiteralPath $deployZip -Force
}

dotnet publish $projectFile `
    --configuration Release `
    --output $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "La publicación Release falló. No se creó el ZIP de Azure."
}

Compress-Archive `
    -Path (Join-Path $publishDirectory "*") `
    -DestinationPath $deployZip `
    -CompressionLevel Optimal

Write-Host "Paquete creado correctamente:" -ForegroundColor Green
Write-Host $deployZip
