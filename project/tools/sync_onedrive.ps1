<#
    Синхронизация сборки и данных в облачную копию OneDrive.

    Копия нужна как резервная: с неё можно запустить билдер на другом
    компьютере, даже если диск D: недоступен. Скрипт копирует актуальную
    сборку, пакет данных, скриншоты и вспомогательные скрипты.

    Запуск:  powershell -ExecutionPolicy Bypass -File tools\sync_onedrive.ps1
#>

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot          # D:\RogueTraderBuilder\project
$base = Split-Path -Parent $root                  # D:\RogueTraderBuilder
$oneDrive = Join-Path $env:USERPROFILE 'OneDrive'

if (-not (Test-Path $oneDrive)) {
    Write-Host "Папка OneDrive не найдена: $oneDrive" -ForegroundColor Red
    exit 1
}

# Ищем папку RogueTraderBuilder в облачной копии.
$dst = Get-ChildItem $oneDrive -Recurse -Depth 3 -Directory -Filter 'RogueTraderBuilder' -ErrorAction SilentlyContinue |
       Select-Object -First 1
if (-not $dst) {
    Write-Host "В OneDrive нет папки RogueTraderBuilder — создаю: $oneDrive\RogueTraderBuilder" -ForegroundColor Yellow
    $dstPath = Join-Path $oneDrive 'RogueTraderBuilder'
    New-Item -ItemType Directory -Force -Path $dstPath | Out-Null
} else {
    $dstPath = $dst.FullName
}

Write-Host "Копирую в: $dstPath" -ForegroundColor Cyan

function Copy-Tree {
    param([string]$From, [string]$To, [string[]]$ExcludeDirs = @())
    if (-not (Test-Path $From)) {
        Write-Host "  пропуск (нет источника): $From" -ForegroundColor DarkGray
        return
    }
    New-Item -ItemType Directory -Force -Path $To | Out-Null
    $args = @($From, $To, '/E', '/R:1', '/W:1', '/NFL', '/NDL', '/NJH', '/NJS', '/NP')
    if ($ExcludeDirs.Count -gt 0) { $args += '/XD'; $args += $ExcludeDirs }
    & robocopy @args | Out-Null
    if ($LASTEXITCODE -ge 8) {
        Write-Host "  ОШИБКА копирования $From -> $To (код $LASTEXITCODE)" -ForegroundColor Red
    } else {
        Write-Host "  готово: $From -> $To" -ForegroundColor Green
    }
}

Copy-Tree (Join-Path $base 'portable')    (Join-Path $dstPath 'portable')
Copy-Tree (Join-Path $root 'data')        (Join-Path $dstPath 'data')
Copy-Tree (Join-Path $root 'tools')      (Join-Path $dstPath 'tools')
Copy-Tree (Join-Path $base 'screenshots') (Join-Path $dstPath 'screenshots')
# Исходники приложения и документация — чтобы облачная копия была полной.
Copy-Tree (Join-Path $root 'src')         (Join-Path $dstPath 'src') @('bin', 'obj')
Copy-Tree $root                          (Join-Path $dstPath 'project') @('data', 'tools', 'src', 'bin', 'obj')

Write-Host "Готово." -ForegroundColor Cyan
