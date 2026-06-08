# Regenera Data/xiaomi_codenames.json desde https://mifirm.net/ (lista pública de modelos).
# Uso: powershell -ExecutionPolicy Bypass -File tools\GenerateCodenamesFromMiFirm.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$outPath = Join-Path $root 'Data\xiaomi_codenames.json'

function Clean-Name([string]$raw) {
    $n = ($raw -replace '\s+', ' ').Trim()
    if ($n -match '/') {
        if ($n -match 'only/|/POCO |/Pocophone |/Redmi |/Xiaomi |/Mi Pad |/Mi Note |/Mi \d|/POCO') {
            $n = ($n -split '/')[-1].Trim()
        }
    }
    $n = $n -replace '\s+Global\s*$', ''
    $n = $n -replace '\s+China\s*$', ''
    $n = $n -replace '\s+India only\s*$', ''
    return $n.Trim()
}

$overrides = @{
    curtana   = 'Redmi Note 9S'
    joyeuse   = 'Redmi Note 9 Pro'
    excalibur = 'Redmi Note 9 Pro Max'
    alioth    = 'POCO F3'
    haydn     = 'Mi 11i'
    monet     = 'Mi 10 Lite 5G'
    toco      = 'Mi Note 10 Lite'
    sweetin   = 'Redmi Note 10 Lite'
    miel      = 'Redmi 10'
    frost     = 'POCO C40'
    star      = 'Mi 11 Ultra'
    gram      = 'POCO M2 Pro'
    sweet     = 'Redmi Note 10 Pro'
    mondrian  = 'POCO F5 Pro'
    vermeer   = 'POCO F6 Pro'
    zorn      = 'POCO F7 Pro'
}

$html = (Invoke-WebRequest -Uri 'https://mifirm.net/' -UseBasicParsing -TimeoutSec 90).Content
$pattern = '<h5>\s*(.*?)\s*<br\s*/>\s*<div class="mini-model-code">\(([a-zA-Z0-9_]+)\)</div>'
$matches = [regex]::Matches($html, $pattern)

$map = @{}
foreach ($m in $matches) {
    $code = $m.Groups[2].Value.Trim().ToLower()
    $name = Clean-Name $m.Groups[1].Value
    if (-not $map.ContainsKey($code)) { $map[$code] = $name }
}
foreach ($k in $overrides.Keys) { $map[$k] = $overrides[$k] }
if (-not $map.ContainsKey('sweetin')) { $map['sweetin'] = 'Redmi Note 10 Lite' }

New-Item -ItemType Directory -Force -Path (Split-Path $outPath) | Out-Null
$sorted = $map.GetEnumerator() | Sort-Object Name
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('{')
$i = 0
foreach ($e in $sorted) {
    $i++
    $comma = if ($i -lt $sorted.Count) { ',' } else { '' }
    $key = $e.Key -replace '\\', '\\' -replace '"', '\"'
    $val = $e.Value -replace '\\', '\\' -replace '"', '\"'
    [void]$sb.AppendLine("  `"$key`": `"$val`"$comma")
}
[void]$sb.Append('}')
[System.IO.File]::WriteAllText($outPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
Write-Host "OK: $($map.Count) codenames -> $outPath"
