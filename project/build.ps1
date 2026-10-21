<#
.SYNOPSIS
    Сборка Rogue Trader Builder: portable-версия в один файл + обычная сборка.
.EXAMPLE
    .\build.ps1                 # portable
    .\build.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$OutputDir = 'dist\portable',
    [switch]$SkipDataCheck
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    $msg" -ForegroundColor Green }

Write-Step "Проверка .NET SDK"
$sdk = dotnet --version 2>$null
if (-not $sdk) { throw "Не найден dotnet. Установите .NET SDK 10." }
Write-Ok ".NET SDK $sdk"

# ── Данные: если пака нет — напоминаем, как его получить ──
if (-not $SkipDataCheck) {
    Write-Step "Проверка дата-пака"
    if (-not (Test-Path 'data\builtin\01-characteristics.json')) {
        Write-Host "    ВНИМАНИЕ: data\builtin пуста." -ForegroundColor Yellow
        Write-Host "    Сгенерируйте пак:  python tools\build_datapack.py --raw tools\raw --out data\builtin" -ForegroundColor Yellow
    } else {
        $n = (Get-ChildItem 'data\builtin\*.json' -ErrorAction SilentlyContinue).Count
        Write-Ok "файлов данных: $n"
    }
}

# ── Portable ──
Write-Step "Сборка portable-версии ($Configuration)"
$outFull = Join-Path $root $OutputDir
if (Test-Path $outFull) { Remove-Item $outFull -Recurse -Force }

dotnet publish src\RogueTrader.App\RogueTrader.App.csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outFull `
    --nologo -v minimal

if ($LASTEXITCODE -ne 0) { throw "Сборка не удалась (код $LASTEXITCODE)" }

$exe = Join-Path $outFull 'RogueTraderBuilder.exe'
if (-not (Test-Path $exe)) { throw "Не найден $exe" }

$mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Ok "RogueTraderBuilder.exe — $mb МБ"
Write-Ok "папка: $outFull"

Write-Step "Готово. Запуск: $exe"
Write-Host ""
Write-Host "Перенесите папку '$OutputDir' целиком — приложение portable, установка не нужна." -ForegroundColor Green