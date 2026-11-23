# Полная резервная копия БД ElectroFix+ (логическая, pg_dump в формате custom).
#
# Копирую только схему public: там все мои таблицы, функции, триггеры и представления.
# Служебные схемы Supabase (auth, storage и т. д.) мне не нужны, и без них копия
# разворачивается на любом обычном PostgreSQL.
#
# Пароль в скрипте не храню. pg_dump берёт его сам из %APPDATA%\postgresql\pgpass.conf
# (для запуска по расписанию) или из переменной $env:PGPASSWORD (для ручного запуска).
#
# Примеры:
#   .\db\backup.ps1 -Db "postgresql://postgres@localhost:5433/electrofix_dev"
#   .\db\backup.ps1      # адрес берётся из переменной окружения ELECTROFIX_DB_URL

param(
    # куда подключаться: postgresql://пользователь@хост:порт/база (для Supabase ещё ?sslmode=require)
    [string]$Db = $env:ELECTROFIX_DB_URL,
    # копии с персональными данными клиентов держу вне репозитория
    [string]$OutDir = (Join-Path $env:USERPROFILE 'ElectroFix-backups'),
    # сколько дней хранить копии (обоснование в db/README.md)
    [ValidateRange(1, 3650)]
    [int]$KeepDays = 14
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Db)) {
    throw 'Не указан адрес базы: передайте -Db или задайте переменную ELECTROFIX_DB_URL'
}

# pg_dump ищу в PATH, а если его там нет — в установленном PostgreSQL (беру самую новую версию)
function Find-PgTool([string]$name) {
    $cmd = Get-Command "$name.exe" -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $found = Get-ChildItem 'C:\Program Files\PostgreSQL' -Directory -ErrorAction SilentlyContinue |
        Sort-Object { [int]($_.Name -replace '\D', '') } -Descending |
        ForEach-Object { Join-Path $_.FullName "bin\$name.exe" } |
        Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) { throw "Не найден $name.exe. Установите PostgreSQL 17 или добавьте его bin в PATH" }
    return $found
}

$pgDump = Find-PgTool 'pg_dump'
$pgRestore = Find-PgTool 'pg_restore'

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$file = Join-Path $OutDir ("electrofix_{0:yyyy-MM-dd_HHmm}.dump" -f (Get-Date))
$log = Join-Path $OutDir 'backup.log'

function Write-Log([string]$text) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $text
    Write-Host $line
    Add-Content -Path $log -Value $line -Encoding UTF8
}

# в лог пишу адрес без возможного пароля, на всякий случай вырезаю всё между ':' и '@'
$safeDb = $Db -replace '://([^:@/]+):[^@]*@', '://$1:***@'
Write-Log "Начинаю копию $safeDb"

$watch = [Diagnostics.Stopwatch]::StartNew()
& $pgDump --dbname=$Db --schema=public --format=custom --file=$file
if ($LASTEXITCODE -ne 0) {
    # недоделанный файл убираю (Remove-Item в PowerShell 5.1 не любит пути вида MYPC~1)
    [IO.File]::Delete($file)
    Write-Log "ОШИБКА: pg_dump завершился с кодом $LASTEXITCODE"
    exit 1
}

# проверяю, что файл читается: pg_restore --list разбирает оглавление копии
$toc = & $pgRestore --list $file
if ($LASTEXITCODE -ne 0) {
    Write-Log 'ОШИБКА: копия создана, но pg_restore не может её прочитать'
    exit 1
}
$tables = ($toc | Select-String ' TABLE DATA ').Count
$sizeKb = [math]::Round((Get-Item $file).Length / 1KB)
Write-Log ("Готово: {0}, {1} КБ, таблиц с данными: {2}, {3:N1} с" -f (Split-Path $file -Leaf), $sizeKb, $tables, $watch.Elapsed.TotalSeconds)

# старые копии удаляю, чтобы папка не росла бесконечно. Только что сделанную не трогаю никогда
$old = Get-ChildItem $OutDir -Filter 'electrofix_*.dump' |
    Where-Object { $_.FullName -ne $file -and $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) }
foreach ($f in $old) {
    [IO.File]::Delete($f.FullName)
    Write-Log "Удалена старая копия $($f.Name) (старше $KeepDays дн.)"
}
exit 0
