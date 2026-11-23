# Проверка восстановления из резервной копии (еженедельный restore-тест).
#
# Что делаю:
#   1) беру копию (по умолчанию самую свежую из папки бэкапов);
#   2) создаю пустую проверочную базу и разворачиваю в неё копию через pg_restore;
#   3) считаю в ней строки во всех таблицах, функции, триггеры, представления,
#      проверяю, что RLS включён везде и все внешние ключи на месте;
#   4) если указан исходный сервер — то же самое считаю там и сравниваю;
#   5) пишу протокол в папку бэкапов и удаляю проверочную базу.
# Рабочую базу скрипт не трогает: восстановление идёт только в отдельную базу.
#
# Пароли — как и в backup.ps1, из pgpass.conf или $env:PGPASSWORD.
#
# Пример:
#   .\db\restore-test.ps1 -Server "postgresql://postgres@localhost:5433" -Source $env:ELECTROFIX_DB_URL

param(
    # какую копию проверять; если не указано — самую свежую
    [string]$File,
    [string]$BackupDir = (Join-Path $env:USERPROFILE 'ElectroFix-backups'),
    # сервер, на котором разворачиваю проверочную базу (без имени базы в конце)
    [string]$Server = 'postgresql://postgres@localhost:5433',
    [string]$TestDb = 'electrofix_restore_test',
    # с какой базой сверять (обычно та, с которой делали копию); можно не указывать
    [string]$Source = $env:ELECTROFIX_DB_URL,
    # оставить проверочную базу после теста, чтобы посмотреть её руками
    [switch]$Keep
)

$ErrorActionPreference = 'Stop'

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

$psql = Find-PgTool 'psql'
$pgRestore = Find-PgTool 'pg_restore'

if (-not $File) {
    $latest = Get-ChildItem $BackupDir -Filter 'electrofix_*.dump' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $latest) { throw "В папке $BackupDir нет копий. Сначала запустите db\backup.ps1" }
    $File = $latest.FullName
}

$Server = $Server.TrimEnd('/')
$adminDb = "$Server/postgres"
$testUrl = "$Server/$TestDb"
$report = New-Object System.Collections.Generic.List[string]

function Say([string]$text) {
    Write-Host $text
    $report.Add($text)
}

function Run-Psql([string]$db, [string]$sqlFile) {
    $out = & $psql --dbname=$db --no-psqlrc --tuples-only --no-align --quiet -v ON_ERROR_STOP=1 --file=$sqlFile
    if ($LASTEXITCODE -ne 0) { throw "psql завершился с кодом $LASTEXITCODE ($db)" }
    return $out
}

# одна выборка на всё: строки по таблицам + объекты схемы. Строки считаю через query_to_xml,
# чтобы не писать отдельный count(*) на каждую из 16 таблиц
$statsSql = @'
SELECT 'Строк в ' || table_name || ': ' ||
       (xpath('/row/n/text()', query_to_xml(format('SELECT count(*) AS n FROM public.%I', table_name), false, true, '')))[1]::text
  FROM information_schema.tables
 WHERE table_schema = 'public' AND table_type = 'BASE TABLE'
UNION ALL
SELECT 'Функций и процедур: ' || count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'public'
UNION ALL
SELECT 'Триггеров: ' || count(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND NOT t.tgisinternal
UNION ALL
SELECT 'Представлений: ' || count(*) FROM information_schema.views WHERE table_schema = 'public'
UNION ALL
SELECT 'Внешних ключей: ' || count(*) FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace
 WHERE n.nspname = 'public' AND c.contype = 'f'
UNION ALL
SELECT 'Таблиц без RLS: ' || count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE n.nspname = 'public' AND c.relkind = 'r' AND NOT c.relrowsecurity
UNION ALL
SELECT 'Последняя миграция: ' || max(migration_id) FROM public."__EFMigrationsHistory"
ORDER BY 1;
'@
$sqlFile = Join-Path $env:TEMP 'electrofix-restore-stats.sql'
Set-Content -Path $sqlFile -Value $statsSql -Encoding UTF8

$watch = [Diagnostics.Stopwatch]::StartNew()
Say ('Проверка восстановления {0:yyyy-MM-dd HH:mm}' -f (Get-Date))
Say "Копия: $(Split-Path $File -Leaf) ($([math]::Round((Get-Item $File).Length / 1KB)) КБ)"
Say "Проверочная база: $TestDb"

# 1) пустая проверочная база (если осталась от прошлого раза — пересоздаю)
$createSql = Join-Path $env:TEMP 'electrofix-restore-create.sql'
Set-Content -Path $createSql -Encoding UTF8 -Value @"
SET client_min_messages = warning;
DROP DATABASE IF EXISTS $TestDb WITH (FORCE);
CREATE DATABASE $TestDb;
"@
Run-Psql $adminDb $createSql | Out-Null
# в новой базе уже есть пустая схема public, а копия создаёт её сама — убираю, иначе конфликт
Set-Content -Path $createSql -Encoding UTF8 -Value 'DROP SCHEMA public CASCADE;'
Run-Psql $testUrl $createSql | Out-Null

# 2) восстановление. Владельцев и GRANT не переношу: на проверочном сервере может не быть
# ролей Supabase и electrofix_stats, а для проверки данных они не нужны
& $pgRestore --dbname=$testUrl --no-owner --no-privileges --exit-on-error $File
if ($LASTEXITCODE -ne 0) {
    Say "РЕЗУЛЬТАТ: ОШИБКА — pg_restore завершился с кодом $LASTEXITCODE"
    $ok = $false
}
else {
    Say ('Восстановлено за {0:N1} с' -f $watch.Elapsed.TotalSeconds)

    # 3) что получилось в проверочной базе
    $restored = @(Run-Psql $testUrl $sqlFile | Where-Object { $_ })
    $ok = $true
    if ($restored | Where-Object { $_ -match '^Таблиц без RLS: [1-9]' }) {
        Say 'ВНИМАНИЕ: после восстановления есть таблицы без RLS'
        $ok = $false
    }

    # 4) сверка с исходной базой (если её указали)
    if ($Source) {
        $original = @(Run-Psql $Source $sqlFile | Where-Object { $_ })
        Say ''
        Say ('{0,-45} {1,-12} {2}' -f 'Показатель', 'Копия', 'Исходная')
        foreach ($line in $restored) {
            $name, $value = $line -split ': ', 2
            $origLine = $original | Where-Object { $_ -like "${name}: *" } | Select-Object -First 1
            $origValue = if ($origLine) { ($origLine -split ': ', 2)[1] } else { '—' }
            $mark = if ($value -eq $origValue) { '' } else { '  <-- расходится'; $ok = $false }
            Say ('{0,-45} {1,-12} {2}{3}' -f $name, $value, $origValue, $mark)
        }
        Say 'Если с момента копии в исходной базе что-то меняли, строки могут расходиться — это нормально.'
    }
    else {
        Say ''
        $restored | ForEach-Object { Say $_ }
    }
}

if (-not $Keep) {
    Set-Content -Path $createSql -Encoding UTF8 -Value "DROP DATABASE IF EXISTS $TestDb WITH (FORCE);"
    Run-Psql $adminDb $createSql | Out-Null
}
# Remove-Item в PowerShell 5.1 спотыкается о короткие пути вида C:\Users\MYPC~1, удаляю напрямую
foreach ($tmp in $sqlFile, $createSql) { [IO.File]::Delete($tmp) }

Say ''
Say ($(if ($ok) { 'РЕЗУЛЬТАТ: УСПЕШНО' } else { 'РЕЗУЛЬТАТ: ЕСТЬ РАСХОЖДЕНИЯ, см. выше' }))

New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
$reportFile = Join-Path $BackupDir ("restore-test_{0:yyyy-MM-dd_HHmm}.txt" -f (Get-Date))
$report | Set-Content -Path $reportFile -Encoding UTF8
Write-Host "Протокол: $reportFile"
exit $(if ($ok) { 0 } else { 1 })
