<#
.SYNOPSIS
    Разворачивает дата-пак и «сырые» данные книг в проект.

.DESCRIPTION
    Данные из книг лежат в корне диска D:, рядом с проектом:

        portable\data\builtin\*.json   — готовый дата-пак (его читает приложение)
        raw\*.json                     — сырые извлечения из PDF (для пересборки пака)

    Скрипт копирует их в корень проекта. Запускать один раз.

.EXAMPLE
    .\tools\fetch_data.ps1
#>
[CmdletBinding()]
param(
    [string]$Source = 'D:\RogueTraderBuilder'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

if (-not (Test-Path $Source)) {
    Write-Host "Не найден источник данных: $Source" -ForegroundColor Red
    Write-Host "Папка с data\builtin и tools\raw должна быть рядом с проектом." -ForegroundColor Yellow
    exit 1
}

Write-Host "==> Копирование данных" -ForegroundColor Cyan

foreach ($pair in @(
    @{ From = 'portable\data\builtin'; To = 'data\builtin' },
    @{ From = 'raw';                To = 'tools\raw' }
)) {
    $src = Join-Path $Source $pair.From
    $dst = Join-Path $root $pair.To
    if (-not (Test-Path $src)) { Write-Host "    пропуск: $src не найден" -ForegroundColor Yellow; continue }
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP | Out-Null
    $n = (Get-ChildItem $dst -Filter *.json -ErrorAction SilentlyContinue).Count
    Write-Host "    $dst — файлов: $n" -ForegroundColor Green
}

if (-not (Test-Path 'data\builtin\01-characteristics.json')) {
    Write-Host "Дата-пак не развёрнут." -ForegroundColor Red; exit 1
}

Write-Host ""
Write-Host "Готово. Можно собирать:  .\build.ps1" -ForegroundColor Green