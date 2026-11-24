# ElectroFix+ — информационная система сети сервисных центров

[![CI](https://github.com/WldBus/electrofix-plus/actions/workflows/ci.yml/badge.svg)](https://github.com/WldBus/electrofix-plus/actions/workflows/ci.yml)

Курсовой проект по МДК 11.01 «Технология разработки и защиты баз данных».
Автор: Червяков Даниил Михайлович, группа П-3-23.

Система для сети сервисных центров, которые ремонтируют бытовую технику и электронику. Что в ней есть:
- прайс по группам техники, заявка на ремонт без регистрации и проверка статуса ремонта по номеру квитанции;
- запись клиента на приём;
- приёмка устройства и заказ-наряд;
- смета, которую согласует клиент;
- назначение мастера и ремонт;
- склад запчастей;
- счёт и оплата;
- гарантия;
- журнал аудита.

## Ссылки

| Что | Адрес |
|---|---|
| Сайт (клиенты и сотрудники) | https://electrofix-plus-web.onrender.com |
| Админ-панель | https://electrofix-plus-admin.onrender.com |
| Api (Swagger) | https://electrofix-plus-api.onrender.com/swagger |
| Сервис статистики | https://electrofix-plus-stats.onrender.com/api/health |

> Сервисы стоят на бесплатном тарифе Render и засыпают через 15 минут без запросов.
> Первый заход после сна ждёт около минуты. Если сайт показал «Сервер ответил 502/503»,
> значит Api ещё просыпается: подождите полминуты и нажмите «Повторить».

## Учётные записи для проверки

У всех пароль `ElectroFix2026!`.

| Роль | Email | Где работает |
|---|---|---|
| Администратор БД | `admin@electrofix.ru` | админ-панель |
| Приёмщик | `petrova@electrofix.ru` | сайт → «Заявки», «Записи», «Клиенты» |
| Мастер | `sokolov@electrofix.ru` | сайт → «Мои заказ-наряды» |
| Закупщик | `kuznetsov@electrofix.ru` | сайт → «Склад» |
| Клиент | `novikov.sp@mail.ru` | сайт → «Личный кабинет» |

Новый клиент может зарегистрироваться на сайте сам или оставить заявку без регистрации — её обработает приёмщик. Сотрудников заводит администратор в админ-панели.

Статус ремонта на главной без входа: номер квитанции и 4 последние цифры телефона клиента, например `ЗН-2026-00002` и `5678`.
После 5 неверных попыток номер закрывается на 15 минут, а открытые формы ограничены по частоте запросов с одного IP.

У сайта и админ-панели есть светлая и тёмная тема: по умолчанию как в системе, переключатель в шапке (в админке — внизу меню).

## Схема

```
Клиент, сотрудник ──► Web   (ASP.NET Core MVC) ───┐
                                                  ├──► Api (ASP.NET Core Web API) ──► PostgreSQL (Supabase)
Администратор ─────► Admin (ASP.NET Core MVC) ────┤                                        ▲
                                                  └──► Stats (микросервис статистики) ─────┘ только чтение
```

- **К базе подключается только Api.** Web и Admin получают данные от него по HTTP через типизированный `ApiClient`.
- **Stats** читает базу под отдельной ролью `electrofix_stats`. Ей видны только представления статистики, таблицы нет.
- **Вход.** Api выдаёт JWT, который живёт 15 минут, и refresh-токен на 7 дней. Web и Admin кладут оба токена в свою cookie: HttpOnly, Secure, SameSite=Lax. `TokenHandler` подставляет `Bearer` в каждый запрос к Api, а на ответ 401 один раз обновляет токен.

### Проекты

```
ElectroFix.sln
├─ src/ElectroFix.Shared   DTO, роли, подписи статусов, правила переходов (без EF и без БД)
├─ src/ElectroFix.Api      Web API: EF Core + Npgsql, JWT, сервисы с бизнес-правилами, Swagger
├─ src/ElectroFix.Admin    админ-панель: CRUD, корзина, пользователи, журнал, статистика
├─ src/ElectroFix.Web      сайт: прайс, кабинет клиента, места приёмщика, мастера, закупщика
├─ src/ElectroFix.Stats    микросервис статистики для графиков админ-панели
├─ tests/ElectroFix.Api.Tests   xUnit: модульные и интеграционные тесты (Testcontainers)
└─ db/                     schema.sql (из миграций) и roles.sql (роли БД)
```

### База данных

Таблиц 16:
- `roles`, `users`, `refresh_tokens`;
- `clients`, `devices`, `checkins`;
- `work_orders`, `services`, `parts`, `order_services`, `order_parts`;
- `invoices`, `payments`, `warranty_cases`, `stock_moves`;
- `audit_log`.

Что ещё есть в схеме:
- **Ограничения:** внешние ключи, уникальные индексы, CHECK.
- **Логика на PL/pgSQL:** триггеры переходов статусов, аудита и складских остатков, процедуры `sp_*`, представления `v_*`.
- **Корзина** у клиентов, работ, запчастей и пользователей: поле `deleted_at` и глобальный фильтр EF.
- **RLS** включён на всех таблицах без правил. Владельцу (Api) он не мешает, а всем остальным закрывает доступ.

Схема создаётся **миграциями EF Core**. Первая миграция `Initial` выполняет SQL-скрипты из `src/ElectroFix.Api/Data/Sql`.

**Api сам применяет миграции при старте** (`Database.Migrate()`). Если база пустая, он заливает тестовые данные из `seed.sql`. Поэтому на новой базе ничего вручную запускать не нужно.

Скрипт для отчёта пересобирается после каждой миграции. Инструмент `dotnet-ef` закреплён в `dotnet-tools.json`, поэтому сначала выполните `dotnet tool restore`:
```bash
dotnet tool restore
dotnet ef migrations script --idempotent --project src/ElectroFix.Api -o db/schema.sql
```

## Запуск на своём ПК

Что нужно установить:
- .NET SDK 10;
- PostgreSQL 17, локально или в Docker;
- Docker Desktop — только для тестов.

### 1. База

Подойдёт любой PostgreSQL 17. Например, в Docker:
```bash
docker run -d --name electrofix-db -e POSTGRES_PASSWORD=<пароль> -p 5433:5432 postgres:17
```
Создавать базу и таблицы не нужно: Api сделает это при первом запуске.

### 2. Секреты

Пароли и ключи в репозиторий не попадают. В `appsettings.json` для них только пустые заглушки, а настоящие значения задаются через `dotnet user-secrets`:

| Проект | Ключ | Что это |
|---|---|---|
| ElectroFix.Api | `ConnectionStrings:Default` | строка подключения владельца БД |
| ElectroFix.Api | `Jwt:Key` | ключ подписи JWT, не короче 32 символов |
| ElectroFix.Stats | `ConnectionStrings:Stats` | строка подключения под ролью `electrofix_stats` |
| ElectroFix.Stats | `Jwt:Key` | тот же ключ, что у Api |

Пример команд (значения свои):
```bash
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5433;Database=electrofix_dev;Username=postgres;Password=<пароль>" --project src/ElectroFix.Api
dotnet user-secrets set "Jwt:Key" "<длинный случайный ключ>" --project src/ElectroFix.Api
```

Роль `electrofix_stats` создаётся скриптом `db/roles.sql`. Запускать его нужно после первого запуска Api, когда в базе уже есть представления статистики. Перед запуском замените в скрипте пароль-заглушку:
```bash
psql -h localhost -p 5433 -U postgres -d electrofix_dev -f db/roles.sql
```

Адреса Api и Stats для Web и Admin уже прописаны в `appsettings.Development.json` (`Api:BaseUrl`, `Stats:BaseUrl`).

### 3. Запуск

В Rider или Visual Studio сделайте конфигурацию, которая запускает несколько проектов сразу: в Rider это Compound, в Visual Studio — Multiple startup projects. Добавьте в неё Api, Stats, Web и Admin. Либо запустите каждый проект в своём терминале:
```bash
dotnet run --project src/ElectroFix.Api
dotnet run --project src/ElectroFix.Stats
dotnet run --project src/ElectroFix.Web
dotnet run --project src/ElectroFix.Admin
```

| Проект | Адрес |
|---|---|
| Api | http://localhost:5146/swagger |
| Stats | http://localhost:5110/api/health |
| Web | http://localhost:5277 |
| Admin | http://localhost:5090 |

### 4. Тесты

```bash
dotnet test
```
Всего 90 тестов:
- **Модульные** (`RulesTests`): бизнес-правила без базы. Переходы статусов по ролям, запись на приём, ИНН, сортировка, формат ошибок.
- **Интеграционные:** `WebApplicationFactory` и настоящий PostgreSQL 17 в Docker (Testcontainers). Вход, 401/403 по ролям, 422 по полям, 409 при конфликтах, фильтры и страницы списков, полный путь ремонта от записи до оплаты, заявка с сайта и проверка статуса по номеру квитанции.

Для интеграционных тестов должен быть запущен Docker Desktop.

## Публикация

Сервисы — на **Render** (4 Web Service, Runtime: Docker), база — в **Supabase** (PostgreSQL).

### Supabase

1. **Проект:** регион Central EU (Frankfurt), **Data API выключен**: в базу ходит только наш Api.
2. **Подключение** — через **Session pooler** (порт 5432). Прямое подключение работает только по IPv6.
   ```
   Host=<host>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<id>;Password=<пароль>;SSL Mode=Require
   ```
3. **Роль для Stats:** после первого запуска Api выполнить `db/roles.sql` в SQL Editor, подставив свой пароль. Скрипт ещё и забирает права у служебных ролей Supabase `anon` и `authenticated`.

### Render

У каждого сервиса: Root Directory — корень репозитория, Dockerfile Path — `./src/<Проект>/Dockerfile`. Dockerfile многоэтапные: сборка на `sdk:10.0`, запуск на `aspnet:10.0` не от root. Порт приходит в переменной `PORT`.

| Сервис | Dockerfile Path | Переменные окружения | Health Check |
|---|---|---|---|
| electrofix-plus-api | `./src/ElectroFix.Api/Dockerfile` | `ConnectionStrings__Default`, `Jwt__Key` | `/api/health` |
| electrofix-plus-stats | `./src/ElectroFix.Stats/Dockerfile` | `ConnectionStrings__Stats`, `Jwt__Key` | `/api/health` |
| electrofix-plus-web | `./src/ElectroFix.Web/Dockerfile` | `Api__BaseUrl` | `/Account/Login` |
| electrofix-plus-admin | `./src/ElectroFix.Admin/Dockerfile` | `Api__BaseUrl`, `Stats__BaseUrl` | `/Account/Login` |

- `ASPNETCORE_ENVIRONMENT=Production` уже задан в Dockerfile.
- За прокси Render включены `ForwardedHeaders`: приложение видит, что запрос пришёл по HTTPS, поэтому cookie получает флаг Secure.
- Render сам пересобирает сервисы после push в `main` (Auto-Deploy).

Образ можно собрать и у себя, из корня репозитория:
```bash
docker build -f src/ElectroFix.Api/Dockerfile -t electrofix-api .
```

### Ограничения бесплатных тарифов

- **Render** усыпляет сервис через 15 минут без запросов, и первый заход ждёт около минуты. После сна или передеплоя нужно войти заново: ключи шифрования cookie живут внутри контейнера.
- **Supabase** ставит проект на паузу после 7 дней без активности. Возобновляется он кнопкой в панели, данные сохраняются.

## Роли БД и резервное копирование

Подробно описано в [db/README.md](db/README.md):
- **Роли и права:** роли PostgreSQL и приложения, RLS.
- **Регламент:** ночная полная копия, хранение 14 дней, еженедельная проверка восстановления.
- **Скрипты:** `db/backup.ps1` и `db/restore-test.ps1`.
- **Порядок восстановления** после сбоя и протокол проверок.

## CI

`.github/workflows/ci.yml` запускается на push и pull request в `main`:

restore → build `-warnaserror` → `dotnet format --verify-no-changes` → `dotnet test`

Секреты в CI не нужны: тесты поднимают свою базу в Docker.
