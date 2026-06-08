# Empaqueta Xiaomi Flash para distribución (producción).
# Uso desde la raíz del proyecto:
#   powershell -ExecutionPolicy Bypass -File tools\Build-Production.ps1
#   powershell -ExecutionPolicy Bypass -File tools\Build-Production.ps1 -Zip
#   powershell -ExecutionPolicy Bypass -File tools\Build-Production.ps1 -Installer
#   powershell -ExecutionPolicy Bypass -File tools\Build-Production.ps1 -Zip -Installer

param(
    [ValidateSet('x64', 'x86')]
    [string]$Platform = 'x64',
    [switch]$Zip,
    [switch]$Installer
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$profileName = if ($Platform -eq 'x64') { 'SelfContained-win-x64' } else { 'SelfContained-win-x86' }
$publishSubdir = if ($Platform -eq 'x64') { 'self-contained-x64' } else { 'self-contained' }
$publishPath = Join-Path $root "publish\$publishSubdir"

Write-Host "==> dotnet publish -p:PublishProfile=$profileName" -ForegroundColor Cyan
dotnet publish Xiaomi_Flash.csproj -p:PublishProfile=$profileName
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

$exe = Join-Path $publishPath 'Xiaomi_Flash.exe'
if (-not (Test-Path $exe)) { throw "Missing $exe after publish." }

@('fastboot.exe', 'AdbWinApi.dll', 'Data\xiaomi_codenames.json') | ForEach-Object {
    $p = Join-Path $publishPath $_
    if (-not (Test-Path $p)) { throw "Publish incomplete: missing $_" }
}

$lzma = if ($Platform -eq 'x64') { 'liblzma64.dll' } else { 'liblzma.dll' }
if (-not (Test-Path (Join-Path $publishPath $lzma))) {
    throw "Publish incomplete: missing $lzma"
}

Write-Host "OK publish: $publishPath" -ForegroundColor Green

if ($Zip) {
    $zipDir = Join-Path $root 'publish\zip'
    New-Item -ItemType Directory -Force -Path $zipDir | Out-Null
    $version = '2.0.1'
    $zipName = "Xiaomi_Flash_${version}_${Platform}_portable.zip"
    $zipPath = Join-Path $zipDir $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "OK zip: $zipPath" -ForegroundColor Green
}

if ($Installer) {
    if ($Platform -ne 'x64') {
        throw 'Installer script is x64-only. Use -Platform x64 or build x86 zip without -Installer.'
    }

    $isccCandidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        Write-Host ""
        Write-Host "Inno Setup 6 not found. Install from: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
        Write-Host "Then run: ISCC.exe tools\XiaomiFlash.iss" -ForegroundColor Yellow
        exit 2
    }

    $iss = Join-Path $root 'tools\XiaomiFlash.iss'
    Write-Host "==> $iscc $iss" -ForegroundColor Cyan
    & $iscc $iss
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed." }

    $setup = Get-ChildItem (Join-Path $root 'publish\installer') -Filter 'Xiaomi_Flash_*_Setup_x64.exe' |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($setup) {
        Write-Host "OK installer: $($setup.FullName)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Done. Ship publish\$publishSubdir\ or publish\installer\ setup exe." -ForegroundColor Cyan
