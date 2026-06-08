# Ofusca Xiaomi_Flash.dll dentro de una carpeta publish ya generada.
# No modifica el código fuente del proyecto.
# Uso: powershell -ExecutionPolicy Bypass -File tools\Obfuscate-Publish.ps1 -PublishDir publish\self-contained-x64

param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$publishPath = if ([IO.Path]::IsPathRooted($PublishDir)) { $PublishDir } else { Join-Path $root $PublishDir }
$dllPath = Join-Path $publishPath 'Xiaomi_Flash.dll'

if (-not (Test-Path $dllPath)) {
    throw "Missing $dllPath. Run Build-Production.ps1 (publish) first."
}

# Herramienta local (dotnet tool manifest) o global obfuscar.console
$useDotnetTool = Test-Path (Join-Path $root '.config\dotnet-tools.json')
if ($useDotnetTool) {
    Push-Location $root
    try { dotnet tool restore 2>&1 | Out-Null } finally { Pop-Location }
}

$outPath = Join-Path $root 'publish\_obfuscar_staging'
if (Test-Path $outPath) { Remove-Item $outPath -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outPath | Out-Null

$template = Get-Content (Join-Path $PSScriptRoot 'obfuscar.xml') -Raw
$configPath = Join-Path $outPath 'obfuscar.run.xml'
$config = $template `
    -replace 'IN_PATH_PLACEHOLDER', ($publishPath -replace '\\', '/') `
    -replace 'OUT_PATH_PLACEHOLDER', ($outPath -replace '\\', '/')
Set-Content -Path $configPath -Value $config -Encoding UTF8

Write-Host "==> Obfuscar: $publishPath\Xiaomi_Flash.dll" -ForegroundColor Cyan
Push-Location $root
try {
    if ($useDotnetTool) {
        & dotnet obfuscar.console $configPath
    } else {
        & obfuscar.console $configPath
    }
} finally {
    Pop-Location
}
if ($LASTEXITCODE -ne 0) { throw "Obfuscar failed (exit $LASTEXITCODE)." }

$obfDll = Join-Path $outPath 'Xiaomi_Flash.dll'
if (-not (Test-Path $obfDll)) { throw "Obfuscar did not produce Xiaomi_Flash.dll" }

$backup = Join-Path $publishPath 'Xiaomi_Flash.dll.pre-obf.bak'
Copy-Item $dllPath $backup -Force
Copy-Item $obfDll $dllPath -Force

$mapSrc = Join-Path $outPath 'Mapping.txt'
if (Test-Path $mapSrc) {
    $mapDest = Join-Path $root 'tools\obfuscar-mapping-last.txt'
    Copy-Item $mapSrc $mapDest -Force
    Write-Host "Mapping saved (dev only): tools\obfuscar-mapping-last.txt" -ForegroundColor DarkGray
}

Remove-Item $outPath -Recurse -Force
Write-Host "OK obfuscated DLL applied to: $publishPath" -ForegroundColor Green
Write-Host "Test Xiaomi_Flash.exe on real hardware before shipping." -ForegroundColor Yellow
