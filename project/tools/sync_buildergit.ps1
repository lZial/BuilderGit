<#
    Синхронизация рабочей копии с папкой для GitHub.

    Куда: D:\RogueTraderBuilder\BuilderGit — репозиторий, который пушится
    на GitHub (https://github.com/lZial/BuilderGit.git).

    Что попадает в копию:
      project\  — исходники, дата-пак, скрипты сборки, README;
      portable\ — собранное приложение (exe + data + шрифт);
      raw\      — сырьё, из которого пересобирается дата-пак;
      books\    — книги, из которых собраны данные.

    Чего в копии нет: логи (*.log, crash.log), скриншоты, персонажи и
    экспорт пользователей (data\characters, data\export), папки сборки
    (bin, obj), служебные папки (.git, .vs, __pycache__).

    Запуск:  powershell -ExecutionPolicy Bypass -File tools\sync_buildergit.ps1
#>

$ErrorActionPreference = 'Stop'

$base = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # D:\RogueTraderBuilder
$src = Join-Path $base 'project'
$dst = Join-Path $base 'BuilderGit'
if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Force -Path $dst | Out-Null }

# Папки, которые не копируем.
$skipDirs = @('bin', 'obj', '.git', '.vs', '__pycache__', 'screenshots', 'characters', 'export')
# Файлы, которые не копируем.
$skipFiles = @('*.log', 'crash.log')

function Sync-Tree {
    param([string]$From, [string]$To, [string]$Label)

    if (-not (Test-Path $From)) { Write-Host "  пропуск (нет источника): $From" -ForegroundColor DarkGray; return }
    New-Item -ItemType Directory -Force -Path $To | Out-Null

    $rcArgs = @($From, $To, '/MIR', '/R:1', '/W:1', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/XD')
    $rcArgs += $skipDirs
    $rcArgs += '/XF'
    $rcArgs += $skipFiles
    & robocopy @rcArgs | Out-Null
    if ($LASTEXITCODE -ge 8) {
        Write-Host "  ОШИБКА копирования $Label (код $LASTEXITCODE)" -ForegroundColor Red
    } else {
        $n = (Get-ChildItem $To -Recurse -File -Force |
                Where-Object { $_.Name -notlike '*.log' }).Count
        Write-Host "  готово: $Label -> $To ($n файлов)" -ForegroundColor Green
    }
}

Write-Host "Копирую в: $dst" -ForegroundColor Cyan
Sync-Tree $src (Join-Path $dst 'project') 'проект'
Sync-Tree (Join-Path $base 'portable') (Join-Path $dst 'portable') 'portable'
Sync-Tree (Join-Path $base 'raw') (Join-Path $dst 'raw') 'raw'
Sync-Tree (Join-Path $base 'books') (Join-Path $dst 'books') 'books'

# Уборка: в копии не должно остаться логов, скриншотов, папок сборки
# и вложенного репозитория (вложенный .git превратил бы project в
# «подмодуль», и файлы не попали бы в коммит).
Write-Host "Убираю лишнее:" -ForegroundColor Cyan
$junk = @(
    (Join-Path $dst 'project\.git'),
    (Join-Path $dst 'project\screenshots'),
    (Join-Path $dst 'project\bin'),
    (Join-Path $dst 'project\obj'),
    (Join-Path $dst 'project\tools\__pycache__'),
    (Join-Path $dst 'portable\data\export'),
    (Join-Path $dst 'portable\data\characters'),
    (Join-Path $dst 'portable\crash.log'),
    (Join-Path $dst 'books\crash.log'),
    (Join-Path $dst 'raw\crash.log')
)
foreach ($j in $junk) {
    if (Test-Path $j) { Remove-Item $j -Recurse -Force; Write-Host "  убрано: $j" -ForegroundColor DarkGray }
}
# Папки сборки внутри src\ — тоже убираем.
Get-ChildItem (Join-Path $dst 'project') -Recurse -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -in @('bin', 'obj', '__pycache__', '.vs', 'screenshots') } |
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force; Write-Host "  убрано: $($_.FullName)" -ForegroundColor DarkGray }
Get-ChildItem $dst -Recurse -File -Force -Include *.log, crash.log -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-Item $_.FullName -Force; Write-Host "  убрано: $($_.FullName)" -ForegroundColor DarkGray }

# Итог
$files = Get-ChildItem $dst -Recurse -File -Force | Where-Object { $_.FullName -notmatch '\\\.git\\' }
$logs = Get-ChildItem $dst -Recurse -File -Force -Include *.log, crash.log -ErrorAction SilentlyContinue
$shots = Get-ChildItem $dst -Recurse -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -eq 'screenshots' }
Write-Host ""
Write-Host "ИТОГО: файлов в копии $($files.Count); логов $($logs.Count); папок со скриншотами $($shots.Count)" -ForegroundColor Cyan
Write-Host "Готово." -ForegroundColor Cyan
