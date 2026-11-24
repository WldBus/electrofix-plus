CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    migration_id character varying(150) NOT NULL,
    product_version character varying(32) NOT NULL,
    CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117113005_Initial') THEN
    -- Таблицы ИС «ElectroFix+» (из БД/ElectroFix_DB.sql, раздел 2)

    -- 2.0 Роли (справочник)
    CREATE TABLE roles (
        role_id  smallint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        code     varchar(20) NOT NULL,
        name     varchar(50) NOT NULL,
        CONSTRAINT uq_roles_code UNIQUE (code),
        CONSTRAINT uq_roles_name UNIQUE (name),
        CONSTRAINT ck_roles_code CHECK (code ~ '^[a-z_]{3,20}$')
    );

    -- 2.1 Пользователи
    CREATE TABLE users (
        user_id        integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        role_id        smallint      NOT NULL,
        full_name      varchar(150)  NOT NULL,
        email          varchar(120)  NOT NULL,
        phone          varchar(20),
        password_hash  varchar(255)  NOT NULL,
        settings       text          NOT NULL DEFAULT '{}',
        created_at     timestamptz     NOT NULL DEFAULT now(),
        CONSTRAINT fk_users_role       FOREIGN KEY (role_id)
            REFERENCES roles (role_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT uq_users_email      UNIQUE (email),
        CONSTRAINT ck_users_full_name  CHECK (length(trim(full_name)) >= 3),
        CONSTRAINT ck_users_email      CHECK (email ~* '^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$'),
        CONSTRAINT ck_users_phone      CHECK (phone IS NULL OR phone ~ '^\+7[0-9]{10}$'),
        CONSTRAINT ck_users_pwd_hash   CHECK (password_hash ~ '^\$2[aby]\$')
    );

    -- 2.2 Клиенты
    CREATE TABLE clients (
        client_id     integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        client_type   varchar(20)   NOT NULL,
        display_name  varchar(150)  NOT NULL,
        inn           varchar(12),
        address       varchar(255),
        phone         varchar(20)   NOT NULL,
        email         varchar(120),
        created_at    timestamptz     NOT NULL DEFAULT now(),
        CONSTRAINT uq_clients_inn        UNIQUE (inn),
        CONSTRAINT ck_clients_type       CHECK (client_type IN ('individual', 'legal_entity', 'management_company')),
        CONSTRAINT ck_clients_name       CHECK (length(trim(display_name)) >= 2),
        CONSTRAINT ck_clients_inn_format CHECK (inn IS NULL OR inn ~ '^([0-9]{10}|[0-9]{12})$'),
        CONSTRAINT ck_clients_inn_legal  CHECK (client_type = 'individual'
                                                OR (inn IS NOT NULL AND length(inn) = 10)),
        CONSTRAINT ck_clients_phone      CHECK (phone ~ '^\+7[0-9]{10}$'),
        CONSTRAINT ck_clients_email      CHECK (email IS NULL OR email ~* '^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$')
    );

    -- 2.3 Устройства
    CREATE TABLE devices (
        device_id         integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        client_id         integer      NOT NULL,
        type              varchar(50)  NOT NULL,
        brand             varchar(80)  NOT NULL,
        model             varchar(100) NOT NULL,
        manufacture_year  integer,
        serial_number     varchar(50)  NOT NULL,
        inventory_number  varchar(50),
        runtime_hours     integer      NOT NULL DEFAULT 0,
        CONSTRAINT fk_devices_client  FOREIGN KEY (client_id)
            REFERENCES clients (client_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT uq_devices_serial  UNIQUE (serial_number),
        CONSTRAINT ck_devices_serial  CHECK (serial_number ~ '^[A-Za-z0-9/-]{4,50}$'),
        CONSTRAINT ck_devices_year    CHECK (manufacture_year IS NULL OR manufacture_year BETWEEN 1950 AND 2100),
        CONSTRAINT ck_devices_runtime CHECK (runtime_hours >= 0)
    );

    -- 2.4 Записи / приёмка (чек-ин). Клиент определяется через устройство.
    CREATE TABLE checkins (
        checkin_id           integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        device_id            integer        NOT NULL,
        receptionist_id      integer,
        branch               varchar(100)   NOT NULL,
        slot_start           timestamptz      NOT NULL,
        slot_end             timestamptz      NOT NULL,
        services_requested   varchar(255),
        notes                text,
        photos               text,
        defects              text,
        condition_state      varchar(255),
        runtime_at_checkin   integer,
        priority             varchar(10)    NOT NULL DEFAULT 'normal',
        status               varchar(20)    NOT NULL DEFAULT 'booked',
        CONSTRAINT fk_checkins_device       FOREIGN KEY (device_id)
            REFERENCES devices (device_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT fk_checkins_receptionist FOREIGN KEY (receptionist_id)
            REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE SET NULL,
        CONSTRAINT ck_checkins_slot         CHECK (slot_end > slot_start
                                                   AND slot_end - slot_start <= interval '8 hours'),
        CONSTRAINT ck_checkins_priority     CHECK (priority IN ('low', 'normal', 'high', 'urgent')),
        CONSTRAINT ck_checkins_runtime      CHECK (runtime_at_checkin IS NULL OR runtime_at_checkin >= 0),
        CONSTRAINT ck_checkins_status       CHECK (status IN ('booked', 'confirmed', 'checked_in',
                                                              'cancelled', 'no_show')),
        CONSTRAINT ck_checkins_receptionist CHECK (status NOT IN ('confirmed', 'checked_in')
                                                   OR receptionist_id IS NOT NULL)
    );

    -- 2.5 Заказ-наряды. Клиент и устройство определяются через запись (checkin),
    --     суммы вычисляются по позициям наряда и не хранятся.
    CREATE TABLE work_orders (
        work_order_id      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        work_order_number  varchar(30)       NOT NULL,
        checkin_id         integer           NOT NULL,
        receptionist_id    integer           NOT NULL,
        master_id          integer,
        status             varchar(20)       NOT NULL DEFAULT 'new',
        opened_at          timestamptz         NOT NULL DEFAULT now(),
        closed_at          timestamptz,
        CONSTRAINT fk_wo_checkin      FOREIGN KEY (checkin_id)
            REFERENCES checkins (checkin_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT fk_wo_receptionist FOREIGN KEY (receptionist_id)
            REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT fk_wo_master       FOREIGN KEY (master_id)
            REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT uq_wo_number       UNIQUE (work_order_number),
        CONSTRAINT uq_wo_checkin      UNIQUE (checkin_id),   -- связь 1:1 с записью
        CONSTRAINT ck_wo_number       CHECK (work_order_number ~ '^ЗН-[0-9]{4}-[0-9]{5}$'),
        CONSTRAINT ck_wo_status       CHECK (status IN ('new', 'diagnostics', 'estimate', 'approved', 'assigned',
                                                        'in_progress', 'pdi', 'ready', 'closed', 'cancelled')),
        CONSTRAINT ck_wo_closed_at    CHECK ((status = 'closed') = (closed_at IS NOT NULL)),
        CONSTRAINT ck_wo_dates        CHECK (closed_at IS NULL OR closed_at >= opened_at),
        CONSTRAINT ck_wo_master       CHECK (status NOT IN ('assigned', 'in_progress', 'pdi', 'ready', 'closed')
                                             OR master_id IS NOT NULL)
    );

    -- 2.6 Справочник работ
    CREATE TABLE services (
        service_id       integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        name             varchar(150)  NOT NULL,
        category         varchar(50)   NOT NULL,
        norm_hours       decimal(6,2)  NOT NULL,
        rate             decimal(12,2) NOT NULL,
        warranty_months  integer       NOT NULL DEFAULT 3,
        CONSTRAINT uq_services_name      UNIQUE (name, category),
        CONSTRAINT ck_services_hours     CHECK (norm_hours > 0 AND norm_hours <= 100),
        CONSTRAINT ck_services_rate      CHECK (rate >= 0),
        CONSTRAINT ck_services_warranty  CHECK (warranty_months BETWEEN 0 AND 36)
    );

    -- 2.7 Номенклатура запчастей
    CREATE TABLE parts (
        part_id    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        article    varchar(50)  NOT NULL,
        name       varchar(150) NOT NULL,
        category   varchar(50)  NOT NULL,
        unit       varchar(10)  NOT NULL DEFAULT 'шт',
        min_stock  integer      NOT NULL DEFAULT 0,
        CONSTRAINT uq_parts_article   UNIQUE (article),
        CONSTRAINT ck_parts_article   CHECK (article ~ '^[A-Za-z0-9._-]{3,50}$'),
        CONSTRAINT ck_parts_unit      CHECK (unit IN ('шт', 'компл', 'м', 'кг', 'л', 'упак')),
        CONSTRAINT ck_parts_min_stock CHECK (min_stock >= 0)
    );

    -- 2.8 Позиции заказ-наряда: работы (M:M work_orders - services)
    CREATE TABLE order_services (
        order_service_id  integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        work_order_id     integer         NOT NULL,
        service_id        integer         NOT NULL,
        estimate_version  integer         NOT NULL DEFAULT 1,
        actual_time       decimal(6,2)    NOT NULL DEFAULT 0,
        approval_status   varchar(10)     NOT NULL DEFAULT 'draft',
        discount_markup   decimal(6,2)    NOT NULL DEFAULT 0,   -- %, <0 скидка, >0 наценка
        is_emergency      boolean         NOT NULL DEFAULT false,
        CONSTRAINT fk_os_work_order FOREIGN KEY (work_order_id)
            REFERENCES work_orders (work_order_id) ON UPDATE CASCADE ON DELETE CASCADE,
        CONSTRAINT fk_os_service    FOREIGN KEY (service_id)
            REFERENCES services (service_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT uq_os_line       UNIQUE (work_order_id, service_id, estimate_version),
        CONSTRAINT ck_os_approval   CHECK (approval_status IN ('draft', 'pending', 'approved', 'rejected')),
        CONSTRAINT ck_os_version    CHECK (estimate_version >= 1),
        CONSTRAINT ck_os_time       CHECK (actual_time >= 0 AND actual_time <= 200),
        CONSTRAINT ck_os_discount   CHECK (discount_markup BETWEEN -50 AND 100)
    );

    -- 2.9 Позиции заказ-наряда: запчасти (M:M work_orders - parts)
    CREATE TABLE order_parts (
        order_part_id    integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        work_order_id    integer         NOT NULL,
        part_id          integer         NOT NULL,
        quantity         decimal(10,2)   NOT NULL,
        price            decimal(12,2)   NOT NULL,   -- цена на момент включения в смету
        batch            varchar(50),
        approval_status  varchar(10)     NOT NULL DEFAULT 'draft',
        CONSTRAINT fk_op_work_order FOREIGN KEY (work_order_id)
            REFERENCES work_orders (work_order_id) ON UPDATE CASCADE ON DELETE CASCADE,
        CONSTRAINT fk_op_part       FOREIGN KEY (part_id)
            REFERENCES parts (part_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT uq_op_line       UNIQUE (work_order_id, part_id, batch),
        CONSTRAINT ck_op_approval   CHECK (approval_status IN ('draft', 'pending', 'approved', 'rejected')),
        CONSTRAINT ck_op_quantity   CHECK (quantity > 0),
        CONSTRAINT ck_op_price      CHECK (price >= 0)
    );

    -- 2.10а Счета. Клиент определяется через заказ-наряд.
    CREATE TABLE invoices (
        invoice_id      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        invoice_number  varchar(30)    NOT NULL,
        work_order_id   integer        NOT NULL,
        amount          decimal(12,2)  NOT NULL,   -- сумма документа, фиксируется при выставлении
        price_list_ref  varchar(50),
        status          varchar(15)    NOT NULL DEFAULT 'unpaid',
        issued_at       timestamptz      NOT NULL DEFAULT now(),
        CONSTRAINT fk_inv_work_order FOREIGN KEY (work_order_id)
            REFERENCES work_orders (work_order_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT uq_inv_number     UNIQUE (invoice_number),
        CONSTRAINT ck_inv_number     CHECK (invoice_number ~ '^СЧ-[0-9]{4}-[0-9]{5}$'),
        CONSTRAINT ck_inv_status     CHECK (status IN ('unpaid', 'partially_paid', 'paid', 'cancelled')),
        CONSTRAINT ck_inv_amount     CHECK (amount > 0)
    );

    -- 2.10б Платежи (1:M к счёту – частичная оплата)
    CREATE TABLE payments (
        payment_id  integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        invoice_id  integer        NOT NULL,
        amount      decimal(12,2)  NOT NULL,
        method      varchar(15)    NOT NULL,
        is_refund   boolean        NOT NULL DEFAULT false,
        paid_at     timestamptz      NOT NULL DEFAULT now(),
        CONSTRAINT fk_pay_invoice FOREIGN KEY (invoice_id)
            REFERENCES invoices (invoice_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT ck_pay_method  CHECK (method IN ('cash', 'online', 'bank_transfer')),
        CONSTRAINT ck_pay_amount  CHECK (amount > 0)
    );

    -- 2.11 Гарантийные случаи. Устройство определяется через заказ-наряд.
    CREATE TABLE warranty_cases (
        warranty_case_id   integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        work_order_id      integer           NOT NULL,
        reason             varchar(255)      NOT NULL,
        inspection_notes   text,
        decision           varchar(15)       NOT NULL DEFAULT 'pending',
        settlement_amount  decimal(12,2)     NOT NULL DEFAULT 0,
        created_at         timestamptz         NOT NULL DEFAULT now(),
        CONSTRAINT fk_wc_work_order FOREIGN KEY (work_order_id)
            REFERENCES work_orders (work_order_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT ck_wc_decision_val CHECK (decision IN ('pending', 'free_repair', 'refusal')),
        CONSTRAINT ck_wc_reason     CHECK (length(trim(reason)) >= 5),
        CONSTRAINT ck_wc_settlement CHECK (settlement_amount >= 0),
        CONSTRAINT ck_wc_decision   CHECK (decision = 'pending' OR inspection_notes IS NOT NULL)
    );

    -- 2.12 Движения склада
    CREATE TABLE stock_moves (
        stock_move_id  integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        part_id        integer         NOT NULL,
        work_order_id  integer,
        move_type      varchar(10)     NOT NULL,
        quantity       decimal(10,2)   NOT NULL,
        batch          varchar(50)     NOT NULL,
        storage_cell   varchar(20),
        performed_by   integer         NOT NULL,
        moved_at       timestamptz       NOT NULL DEFAULT now(),
        CONSTRAINT fk_sm_part         FOREIGN KEY (part_id)
            REFERENCES parts (part_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT fk_sm_work_order   FOREIGN KEY (work_order_id)
            REFERENCES work_orders (work_order_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT fk_sm_performed_by FOREIGN KEY (performed_by)
            REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT,
        CONSTRAINT ck_sm_move_type    CHECK (move_type IN ('receipt', 'reserve', 'issue', 'return', 'write_off')),
        CONSTRAINT ck_sm_quantity     CHECK (quantity > 0),
        CONSTRAINT ck_sm_cell         CHECK (storage_cell IS NULL OR storage_cell ~ '^[A-Z]-[0-9]{2}-[0-9]{2}$'),
        CONSTRAINT ck_sm_work_order   CHECK (move_type NOT IN ('reserve', 'issue', 'return')
                                             OR work_order_id IS NOT NULL),
        CONSTRAINT ck_sm_receipt_cell CHECK (move_type <> 'receipt' OR storage_cell IS NOT NULL)
    );

    -- 2.13 Аудит-лог
    CREATE TABLE audit_log (
        audit_id      integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        user_id       integer,
        action        varchar(50)  NOT NULL,
        object_table  varchar(50)  NOT NULL,
        object_id     integer,
        before_data   text,
        after_data    text,
        ip_address    varchar(45),
        user_agent    varchar(255),
        created_at    timestamptz    NOT NULL DEFAULT now(),
        CONSTRAINT fk_audit_user   FOREIGN KEY (user_id)
            REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE SET NULL,
        CONSTRAINT ck_audit_action CHECK (action IN ('INSERT', 'UPDATE', 'DELETE', 'LOGIN',
                                                     'LOGOUT', 'EXPORT', 'IMPORT', 'BACKUP', 'RESTORE'))
    );

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117113005_Initial') THEN
    -- =====================================================================
    --  ИС сети сервисных центров «ElectroFix+»
    --  Серверная логика базы данных (PostgreSQL 12+)
    --
    --  Выполняется ПОСЛЕ скрипта ElectroFix_DB.sql (таблицы и тестовые данные).
    --  Допускает повторный запуск.
    --
    --  1. Функции              (6)
    --  2. Представления        (5)
    --  3. Хранимые процедуры   (8: CRUD клиента и бизнес-операции)
    --  4. Триггеры             (5)
    --  5. Примеры вызова и проверка (выполняются в транзакции и откатываются)
    -- =====================================================================

    -- =====================================================================
    -- 1. ФУНКЦИИ
    -- =====================================================================

    -- 1.1 Текущий пользователь приложения. Сервер приложения после входа
    --     пользователя выполняет: SELECT set_config('app.user_id', '2', false);
    CREATE OR REPLACE FUNCTION fn_app_user_id() RETURNS integer
    LANGUAGE sql STABLE AS $$
        SELECT nullif(current_setting('app.user_id', true), '')::integer;
    $$;

    -- 1.2 Код роли пользователя
    CREATE OR REPLACE FUNCTION fn_user_role(p_user_id integer) RETURNS varchar
    LANGUAGE sql STABLE AS $$
        SELECT r.code
        FROM users u
        JOIN roles r ON r.role_id = u.role_id
        WHERE u.user_id = p_user_id;
    $$;

    -- 1.3 Итоговая сумма заказ-наряда:
    --     работы последней согласованной версии сметы + согласованные запчасти
    CREATE OR REPLACE FUNCTION fn_work_order_total(p_work_order_id integer) RETURNS numeric
    LANGUAGE sql STABLE AS $$
        SELECT round(
            coalesce((SELECT sum(s.norm_hours * s.rate * (1 + os.discount_markup / 100))
                      FROM order_services os
                      JOIN services s ON s.service_id = os.service_id
                      WHERE os.work_order_id = p_work_order_id
                        AND os.approval_status = 'approved'
                        AND os.estimate_version = (SELECT max(estimate_version)
                                                   FROM order_services
                                                   WHERE work_order_id = p_work_order_id
                                                     AND approval_status = 'approved')), 0)
          + coalesce((SELECT sum(quantity * price)
                      FROM order_parts
                      WHERE work_order_id = p_work_order_id
                        AND approval_status = 'approved'), 0), 2);
    $$;

    -- 1.4 Остаток детали на складе по партии:
    --     приход + возврат - выдача - списание
    CREATE OR REPLACE FUNCTION fn_stock_on_hand(p_part_id integer, p_batch varchar) RETURNS numeric
    LANGUAGE sql STABLE AS $$
        SELECT coalesce(sum(CASE WHEN move_type IN ('receipt', 'return')   THEN quantity
                                 WHEN move_type IN ('issue', 'write_off') THEN -quantity
                                 ELSE 0 END), 0)
        FROM stock_moves
        WHERE part_id = p_part_id AND batch = p_batch;
    $$;

    -- 1.5 Свободный остаток: остаток минус резерв, ещё не выданный в работу
    CREATE OR REPLACE FUNCTION fn_stock_available(p_part_id integer, p_batch varchar) RETURNS numeric
    LANGUAGE sql STABLE AS $$
        SELECT fn_stock_on_hand(p_part_id, p_batch)
             - coalesce((SELECT sum(greatest(reserved, 0))
                         FROM (SELECT sum(CASE move_type WHEN 'reserve' THEN quantity
                                                         WHEN 'issue'   THEN -quantity
                                                         ELSE 0 END) AS reserved
                               FROM stock_moves
                               WHERE part_id = p_part_id AND batch = p_batch
                                 AND work_order_id IS NOT NULL
                               GROUP BY work_order_id) t), 0);
    $$;

    -- 1.6 Оплачено по счёту с учётом возвратов
    CREATE OR REPLACE FUNCTION fn_invoice_paid(p_invoice_id integer) RETURNS numeric
    LANGUAGE sql STABLE AS $$
        SELECT coalesce(sum(CASE WHEN is_refund THEN -amount ELSE amount END), 0)
        FROM payments
        WHERE invoice_id = p_invoice_id;
    $$;

    -- =====================================================================
    -- 2. ПРЕДСТАВЛЕНИЯ
    -- =====================================================================

    -- 2.1 Сводка по заказ-нарядам (клиент и устройство определяются через запись)
    CREATE OR REPLACE VIEW v_work_orders AS
    SELECT wo.work_order_id,
           wo.work_order_number,
           wo.status,
           c.client_id,
           c.display_name                              AS client,
           d.type || ' ' || d.brand || ' ' || d.model  AS device,
           d.serial_number,
           ch.branch,
           r.full_name                                 AS receptionist,
           wo.master_id,
           m.full_name                                 AS master,
           fn_work_order_total(wo.work_order_id)       AS total_amount,
           i.invoice_number,
           i.status                                    AS invoice_status,
           wo.opened_at,
           wo.closed_at
    FROM work_orders wo
    JOIN checkins ch    ON ch.checkin_id = wo.checkin_id
    JOIN devices d      ON d.device_id   = ch.device_id
    JOIN clients c      ON c.client_id   = d.client_id
    JOIN users r        ON r.user_id     = wo.receptionist_id
    LEFT JOIN users m   ON m.user_id     = wo.master_id
    LEFT JOIN invoices i ON i.work_order_id = wo.work_order_id AND i.status <> 'cancelled';

    -- 2.2 Остатки склада по детали и партии
    CREATE OR REPLACE VIEW v_stock_balance AS
    SELECT p.part_id,
           p.article,
           p.name,
           sm.batch,
           max(sm.storage_cell)                          AS storage_cell,
           fn_stock_on_hand(p.part_id, sm.batch)         AS on_hand,
           fn_stock_on_hand(p.part_id, sm.batch)
             - fn_stock_available(p.part_id, sm.batch)   AS reserved,
           fn_stock_available(p.part_id, sm.batch)       AS available
    FROM parts p
    JOIN stock_moves sm ON sm.part_id = p.part_id
    GROUP BY p.part_id, p.article, p.name, sm.batch;

    -- 2.3 Детали, остаток которых ниже минимального запаса
    CREATE OR REPLACE VIEW v_parts_below_min AS
    SELECT p.part_id,
           p.article,
           p.name,
           p.min_stock,
           coalesce(sum(b.on_hand), 0)               AS on_hand,
           p.min_stock - coalesce(sum(b.on_hand), 0) AS to_order
    FROM parts p
    LEFT JOIN v_stock_balance b ON b.part_id = p.part_id
    GROUP BY p.part_id, p.article, p.name, p.min_stock
    HAVING coalesce(sum(b.on_hand), 0) < p.min_stock;

    -- 2.4 Счета: сумма, оплачено, долг
    CREATE OR REPLACE VIEW v_invoices AS
    SELECT i.invoice_id,
           i.invoice_number,
           w.work_order_number,
           w.client,
           i.amount,
           fn_invoice_paid(i.invoice_id)            AS paid,
           i.amount - fn_invoice_paid(i.invoice_id) AS debt,
           i.status,
           i.issued_at
    FROM invoices i
    JOIN v_work_orders w ON w.work_order_id = i.work_order_id;

    -- 2.5 Загрузка мастеров: активные заказ-наряды и нормочасы по ним
    CREATE OR REPLACE VIEW v_master_workload AS
    SELECT u.user_id                                   AS master_id,
           u.full_name                                 AS master,
           count(DISTINCT wo.work_order_id)            AS active_orders,
           coalesce(sum(s.norm_hours), 0)              AS norm_hours,
           coalesce(sum(os.actual_time), 0)            AS actual_hours
    FROM users u
    JOIN roles r ON r.role_id = u.role_id AND r.code = 'master'
    LEFT JOIN work_orders wo ON wo.master_id = u.user_id
                            AND wo.status NOT IN ('closed', 'cancelled')
    LEFT JOIN order_services os ON os.work_order_id = wo.work_order_id
                               AND os.approval_status = 'approved'
    LEFT JOIN services s ON s.service_id = os.service_id
    GROUP BY u.user_id, u.full_name;

    -- =====================================================================
    -- 3. ХРАНИМЫЕ ПРОЦЕДУРЫ
    -- =====================================================================

    -- ---------- CRUD-операции над реестром клиентов ----------
    -- Чтение (R) выполняется запросом SELECT к таблице clients или представлению.

    -- 3.1 Добавление клиента (C). Идентификатор возвращается через p_client_id.
    CREATE OR REPLACE PROCEDURE sp_client_add(
        p_client_type  varchar,
        p_display_name varchar,
        p_phone        varchar,
        p_inn          varchar DEFAULT NULL,
        p_address      varchar DEFAULT NULL,
        p_email        varchar DEFAULT NULL,
        INOUT p_client_id integer DEFAULT NULL)
    LANGUAGE plpgsql AS $$
    BEGIN
        IF p_inn IS NOT NULL AND EXISTS (SELECT 1 FROM clients WHERE inn = p_inn) THEN
            RAISE EXCEPTION 'Клиент с ИНН % уже зарегистрирован', p_inn;
        END IF;

        INSERT INTO clients (client_type, display_name, inn, address, phone, email)
        VALUES (p_client_type, trim(p_display_name), p_inn, p_address, p_phone, p_email)
        RETURNING client_id INTO p_client_id;
    END;
    $$;

    -- 3.2 Изменение контактных данных клиента (U). NULL – поле не меняется.
    CREATE OR REPLACE PROCEDURE sp_client_update(
        p_client_id    integer,
        p_display_name varchar DEFAULT NULL,
        p_phone        varchar DEFAULT NULL,
        p_address      varchar DEFAULT NULL,
        p_email        varchar DEFAULT NULL)
    LANGUAGE plpgsql AS $$
    BEGIN
        UPDATE clients
           SET display_name = coalesce(trim(p_display_name), display_name),
               phone        = coalesce(p_phone, phone),
               address      = coalesce(p_address, address),
               email        = coalesce(p_email, email)
         WHERE client_id = p_client_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'Клиент % не найден', p_client_id;
        END IF;
    END;
    $$;

    -- 3.3 Удаление клиента (D). Клиента с устройствами удалить нельзя.
    CREATE OR REPLACE PROCEDURE sp_client_delete(p_client_id integer)
    LANGUAGE plpgsql AS $$
    BEGIN
        IF EXISTS (SELECT 1 FROM devices WHERE client_id = p_client_id) THEN
            RAISE EXCEPTION 'Клиента % удалить нельзя: за ним числятся устройства', p_client_id;
        END IF;

        DELETE FROM clients WHERE client_id = p_client_id;

        IF NOT FOUND THEN
            RAISE EXCEPTION 'Клиент % не найден', p_client_id;
        END IF;
    END;
    $$;

    -- ---------- Бизнес-операции ----------

    -- 3.4 Открытие заказ-наряда по записи: приёмка устройства и создание наряда
    --     выполняются одной операцией, номер наряда формируется автоматически.
    CREATE OR REPLACE PROCEDURE sp_open_work_order(
        p_checkin_id      integer,
        p_receptionist_id integer,
        INOUT p_work_order_id integer DEFAULT NULL)
    LANGUAGE plpgsql AS $$
    DECLARE
        v_status varchar;
        v_year   text := to_char(now(), 'YYYY');
        v_next   integer;
    BEGIN
        SELECT status INTO v_status FROM checkins WHERE checkin_id = p_checkin_id FOR UPDATE;
        IF v_status IS NULL THEN
            RAISE EXCEPTION 'Запись % не найдена', p_checkin_id;
        END IF;
        IF v_status IN ('cancelled', 'no_show') THEN
            RAISE EXCEPTION 'По записи в статусе % заказ-наряд не открывается', v_status;
        END IF;
        IF EXISTS (SELECT 1 FROM work_orders WHERE checkin_id = p_checkin_id) THEN
            RAISE EXCEPTION 'По записи % заказ-наряд уже открыт', p_checkin_id;
        END IF;

        UPDATE checkins
           SET status = 'checked_in', receptionist_id = p_receptionist_id
         WHERE checkin_id = p_checkin_id;

        SELECT coalesce(max(right(work_order_number, 5)::integer), 0) + 1 INTO v_next
        FROM work_orders
        WHERE work_order_number LIKE 'ЗН-' || v_year || '-%';

        INSERT INTO work_orders (work_order_number, checkin_id, receptionist_id)
        VALUES ('ЗН-' || v_year || '-' || lpad(v_next::text, 5, '0'), p_checkin_id, p_receptionist_id)
        RETURNING work_order_id INTO p_work_order_id;
    END;
    $$;

    -- 3.5 Директивное назначение мастера на заказ-наряд с согласованной сметой
    CREATE OR REPLACE PROCEDURE sp_assign_master(p_work_order_id integer, p_master_id integer)
    LANGUAGE plpgsql AS $$
    DECLARE
        v_status varchar;
    BEGIN
        SELECT status INTO v_status FROM work_orders WHERE work_order_id = p_work_order_id FOR UPDATE;
        IF v_status IS NULL THEN
            RAISE EXCEPTION 'Заказ-наряд % не найден', p_work_order_id;
        END IF;
        IF v_status <> 'approved' THEN
            RAISE EXCEPTION 'Мастер назначается после согласования сметы (текущий статус: %)', v_status;
        END IF;

        UPDATE work_orders
           SET master_id = p_master_id, status = 'assigned'
         WHERE work_order_id = p_work_order_id;
    END;
    $$;

    -- 3.6 Выдача запчасти со склада в работу. Остаток проверяет триггер склада.
    CREATE OR REPLACE PROCEDURE sp_issue_part(
        p_work_order_id integer,
        p_part_id       integer,
        p_batch         varchar,
        p_quantity      numeric,
        p_user_id       integer)
    LANGUAGE plpgsql AS $$
    BEGIN
        INSERT INTO stock_moves (part_id, work_order_id, move_type, quantity, batch, performed_by)
        VALUES (p_part_id, p_work_order_id, 'issue', p_quantity, p_batch, p_user_id);
    END;
    $$;

    -- 3.7 Регистрация платежа или возврата по счёту.
    --     Статус счёта пересчитывает триггер платежей.
    CREATE OR REPLACE PROCEDURE sp_register_payment(
        p_invoice_id integer,
        p_amount     numeric,
        p_method     varchar,
        p_is_refund  boolean DEFAULT false)
    LANGUAGE plpgsql AS $$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM invoices WHERE invoice_id = p_invoice_id) THEN
            RAISE EXCEPTION 'Счёт % не найден', p_invoice_id;
        END IF;

        INSERT INTO payments (invoice_id, amount, method, is_refund)
        VALUES (p_invoice_id, p_amount, p_method, p_is_refund);
    END;
    $$;

    -- 3.8 Закрытие заказ-наряда: проверки, выставление счёта (если его нет)
    --     и перевод в статус closed – одной транзакцией.
    CREATE OR REPLACE PROCEDURE sp_close_work_order(p_work_order_id integer)
    LANGUAGE plpgsql AS $$
    DECLARE
        v_status varchar;
        v_total  numeric;
        v_year   text := to_char(now(), 'YYYY');
        v_next   integer;
    BEGIN
        SELECT status INTO v_status FROM work_orders WHERE work_order_id = p_work_order_id FOR UPDATE;
        IF v_status IS NULL THEN
            RAISE EXCEPTION 'Заказ-наряд % не найден', p_work_order_id;
        END IF;
        IF v_status <> 'ready' THEN
            RAISE EXCEPTION 'Закрыть можно только заказ-наряд, прошедший контроль качества (статус ready), сейчас: %', v_status;
        END IF;
        IF EXISTS (SELECT 1 FROM order_services
                   WHERE work_order_id = p_work_order_id AND approval_status IN ('draft', 'pending'))
           OR EXISTS (SELECT 1 FROM order_parts
                      WHERE work_order_id = p_work_order_id AND approval_status IN ('draft', 'pending')) THEN
            RAISE EXCEPTION 'В заказ-наряде остались несогласованные позиции сметы';
        END IF;

        IF NOT EXISTS (SELECT 1 FROM invoices
                       WHERE work_order_id = p_work_order_id AND status <> 'cancelled') THEN
            v_total := fn_work_order_total(p_work_order_id);
            IF v_total <= 0 THEN
                RAISE EXCEPTION 'Сумма заказ-наряда равна нулю: счёт не может быть выставлен';
            END IF;

            SELECT coalesce(max(right(invoice_number, 5)::integer), 0) + 1 INTO v_next
            FROM invoices
            WHERE invoice_number LIKE 'СЧ-' || v_year || '-%';

            INSERT INTO invoices (invoice_number, work_order_id, amount)
            VALUES ('СЧ-' || v_year || '-' || lpad(v_next::text, 5, '0'), p_work_order_id, v_total);
        END IF;

        UPDATE work_orders SET status = 'closed' WHERE work_order_id = p_work_order_id;
    END;
    $$;

    -- =====================================================================
    -- 4. ТРИГГЕРЫ
    -- =====================================================================

    -- 4.1 Заказ-наряды: роли ответственных, жизненный цикл статусов,
    --     неизменность закрытого наряда, начало работ только при согласованной смете
    CREATE OR REPLACE FUNCTION trg_work_orders_guard() RETURNS trigger
    LANGUAGE plpgsql AS $$
    DECLARE
        v_allowed text[];
    BEGIN
        IF fn_user_role(NEW.receptionist_id) IS DISTINCT FROM 'receptionist' THEN
            RAISE EXCEPTION 'Ответственным за заказ-наряд может быть только мастер-приёмщик';
        END IF;
        IF NEW.master_id IS NOT NULL AND fn_user_role(NEW.master_id) IS DISTINCT FROM 'master' THEN
            RAISE EXCEPTION 'Исполнителем заказ-наряда может быть только пользователь с ролью «Мастер»';
        END IF;

        IF TG_OP = 'INSERT' THEN
            IF NEW.status <> 'new' THEN
                RAISE EXCEPTION 'Заказ-наряд создаётся в статусе new';
            END IF;
            RETURN NEW;
        END IF;

        IF OLD.status IN ('closed', 'cancelled') THEN
            RAISE EXCEPTION 'Заказ-наряд % в статусе % не изменяется', OLD.work_order_number, OLD.status;
        END IF;

        IF NEW.status <> OLD.status THEN
            v_allowed := CASE OLD.status
                WHEN 'new'         THEN ARRAY['diagnostics', 'cancelled']
                WHEN 'diagnostics' THEN ARRAY['estimate', 'cancelled']
                WHEN 'estimate'    THEN ARRAY['approved', 'diagnostics', 'cancelled']
                WHEN 'approved'    THEN ARRAY['assigned', 'estimate', 'cancelled']
                WHEN 'assigned'    THEN ARRAY['in_progress', 'estimate']
                WHEN 'in_progress' THEN ARRAY['pdi', 'estimate']
                WHEN 'pdi'         THEN ARRAY['ready', 'in_progress']
                WHEN 'ready'       THEN ARRAY['closed', 'in_progress']
            END;
            IF NOT (NEW.status = ANY (v_allowed)) THEN
                RAISE EXCEPTION 'Недопустимый переход статуса заказ-наряда: % -> %', OLD.status, NEW.status;
            END IF;

            IF NEW.status IN ('approved', 'in_progress') AND NOT EXISTS (
                SELECT 1 FROM order_services
                WHERE work_order_id = NEW.work_order_id
                  AND (approval_status = 'approved' OR is_emergency)) THEN
                RAISE EXCEPTION 'Нет согласованной сметы: статус % недоступен', NEW.status;
            END IF;

            IF NEW.status = 'closed' THEN
                NEW.closed_at := coalesce(NEW.closed_at, now());
            END IF;
        END IF;

        RETURN NEW;
    END;
    $$;

    DROP TRIGGER IF EXISTS work_orders_guard ON work_orders;
    CREATE TRIGGER work_orders_guard
        BEFORE INSERT OR UPDATE ON work_orders
        FOR EACH ROW EXECUTE FUNCTION trg_work_orders_guard();

    -- 4.2 Склад: запрет отрицательных остатков, резерв только из свободного
    --     остатка, возврат не больше выданного, выдача только деталей из сметы
    CREATE OR REPLACE FUNCTION trg_stock_moves_guard() RETURNS trigger
    LANGUAGE plpgsql AS $$
    DECLARE
        v_on_hand   numeric;
        v_available numeric;
        v_issued    numeric;
    BEGIN
        IF fn_user_role(NEW.performed_by) NOT IN ('purchaser', 'admin') THEN
            RAISE EXCEPTION 'Складские операции выполняет закупщик';
        END IF;

        -- операции по одной детали выполняются строго по очереди
        PERFORM pg_advisory_xact_lock(NEW.part_id);

        v_on_hand   := fn_stock_on_hand(NEW.part_id, NEW.batch);
        v_available := fn_stock_available(NEW.part_id, NEW.batch);

        IF NEW.move_type = 'issue' AND v_on_hand < NEW.quantity THEN
            RAISE EXCEPTION 'Недостаточно остатка: партия %, на складе %, требуется %',
                NEW.batch, v_on_hand, NEW.quantity;
        END IF;

        IF NEW.move_type IN ('reserve', 'write_off') AND v_available < NEW.quantity THEN
            RAISE EXCEPTION 'Недостаточно свободного остатка: партия %, свободно %, требуется %',
                NEW.batch, v_available, NEW.quantity;
        END IF;

        IF NEW.move_type = 'return' THEN
            SELECT coalesce(sum(CASE move_type WHEN 'issue' THEN quantity ELSE -quantity END), 0)
              INTO v_issued
            FROM stock_moves
            WHERE work_order_id = NEW.work_order_id AND part_id = NEW.part_id
              AND batch = NEW.batch AND move_type IN ('issue', 'return');
            IF v_issued < NEW.quantity THEN
                RAISE EXCEPTION 'Возврат % превышает выданное в заказ-наряд количество %', NEW.quantity, v_issued;
            END IF;
        END IF;

        IF NEW.move_type IN ('reserve', 'issue') AND NOT EXISTS (
            SELECT 1 FROM order_parts
            WHERE work_order_id = NEW.work_order_id AND part_id = NEW.part_id
              AND approval_status <> 'rejected') THEN
            RAISE EXCEPTION 'Деталь отсутствует в смете заказ-наряда';
        END IF;

        RETURN NEW;
    END;
    $$;

    DROP TRIGGER IF EXISTS stock_moves_guard ON stock_moves;
    CREATE TRIGGER stock_moves_guard
        BEFORE INSERT ON stock_moves
        FOR EACH ROW EXECUTE FUNCTION trg_stock_moves_guard();

    -- 4.3 Проведённые документы (движения склада, платежи) не редактируются
    --     и не удаляются: исправления вносятся сторнирующей записью
    CREATE OR REPLACE FUNCTION trg_immutable() RETURNS trigger
    LANGUAGE plpgsql AS $$
    BEGIN
        RAISE EXCEPTION 'Таблица %: проведённые записи не изменяются и не удаляются', TG_TABLE_NAME;
    END;
    $$;

    DROP TRIGGER IF EXISTS stock_moves_immutable ON stock_moves;
    CREATE TRIGGER stock_moves_immutable
        BEFORE UPDATE OR DELETE ON stock_moves
        FOR EACH ROW EXECUTE FUNCTION trg_immutable();

    DROP TRIGGER IF EXISTS payments_immutable ON payments;
    CREATE TRIGGER payments_immutable
        BEFORE UPDATE OR DELETE ON payments
        FOR EACH ROW EXECUTE FUNCTION trg_immutable();

    -- 4.4 Платежи: запрет переплаты и возврата сверх оплаченного,
    --     автоматический пересчёт статуса счёта
    CREATE OR REPLACE FUNCTION trg_payments_guard() RETURNS trigger
    LANGUAGE plpgsql AS $$
    DECLARE
        v_amount numeric;
        v_status varchar;
        v_paid   numeric;
    BEGIN
        SELECT amount, status INTO v_amount, v_status
        FROM invoices WHERE invoice_id = NEW.invoice_id FOR UPDATE;

        v_paid := fn_invoice_paid(NEW.invoice_id);

        IF TG_WHEN = 'BEFORE' THEN
            IF v_status = 'cancelled' THEN
                RAISE EXCEPTION 'Счёт отменён: платежи по нему не принимаются';
            END IF;
            IF NOT NEW.is_refund AND v_paid + NEW.amount > v_amount THEN
                RAISE EXCEPTION 'Переплата: к оплате %, уже внесено %, платёж %', v_amount, v_paid, NEW.amount;
            END IF;
            IF NEW.is_refund AND NEW.amount > v_paid THEN
                RAISE EXCEPTION 'Возврат % превышает оплаченную сумму %', NEW.amount, v_paid;
            END IF;
            RETURN NEW;
        END IF;

        -- AFTER INSERT: новый статус счёта
        UPDATE invoices
           SET status = CASE WHEN v_paid <= 0       THEN 'unpaid'
                             WHEN v_paid < v_amount THEN 'partially_paid'
                             ELSE 'paid' END
         WHERE invoice_id = NEW.invoice_id;
        RETURN NULL;
    END;
    $$;

    DROP TRIGGER IF EXISTS payments_check ON payments;
    CREATE TRIGGER payments_check
        BEFORE INSERT ON payments
        FOR EACH ROW EXECUTE FUNCTION trg_payments_guard();

    DROP TRIGGER IF EXISTS payments_status ON payments;
    CREATE TRIGGER payments_status
        AFTER INSERT ON payments
        FOR EACH ROW EXECUTE FUNCTION trg_payments_guard();

    -- 4.5 Аудит: кто, что и когда изменил; состояние записи до и после
    CREATE OR REPLACE FUNCTION trg_audit() RETURNS trigger
    LANGUAGE plpgsql AS $$
    DECLARE
        v_before jsonb := CASE WHEN TG_OP IN ('UPDATE', 'DELETE') THEN to_jsonb(OLD) END;
        v_after  jsonb := CASE WHEN TG_OP IN ('INSERT', 'UPDATE') THEN to_jsonb(NEW) END;
    BEGIN
        INSERT INTO audit_log (user_id, action, object_table, object_id, before_data, after_data)
        VALUES (fn_app_user_id(), TG_OP, TG_TABLE_NAME,
                (coalesce(v_after, v_before) ->> TG_ARGV[0])::integer,
                v_before::text, v_after::text);
        RETURN NULL;
    END;
    $$;

    DROP TRIGGER IF EXISTS audit_clients ON clients;
    CREATE TRIGGER audit_clients AFTER INSERT OR UPDATE OR DELETE ON clients
        FOR EACH ROW EXECUTE FUNCTION trg_audit('client_id');

    DROP TRIGGER IF EXISTS audit_work_orders ON work_orders;
    CREATE TRIGGER audit_work_orders AFTER INSERT OR UPDATE OR DELETE ON work_orders
        FOR EACH ROW EXECUTE FUNCTION trg_audit('work_order_id');

    DROP TRIGGER IF EXISTS audit_invoices ON invoices;
    CREATE TRIGGER audit_invoices AFTER INSERT OR UPDATE OR DELETE ON invoices
        FOR EACH ROW EXECUTE FUNCTION trg_audit('invoice_id');

    DROP TRIGGER IF EXISTS audit_payments ON payments;
    CREATE TRIGGER audit_payments AFTER INSERT ON payments
        FOR EACH ROW EXECUTE FUNCTION trg_audit('payment_id');

    DROP TRIGGER IF EXISTS audit_stock_moves ON stock_moves;
    CREATE TRIGGER audit_stock_moves AFTER INSERT ON stock_moves
        FOR EACH ROW EXECUTE FUNCTION trg_audit('stock_move_id');


    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117113005_Initial') THEN
    -- RLS без правил: на владельца таблиц (подключение Api) не действует,
    -- а всем остальным (например, ролям anon и authenticated в Supabase) закрывает доступ
    ALTER TABLE roles          ENABLE ROW LEVEL SECURITY;
    ALTER TABLE users          ENABLE ROW LEVEL SECURITY;
    ALTER TABLE clients        ENABLE ROW LEVEL SECURITY;
    ALTER TABLE devices        ENABLE ROW LEVEL SECURITY;
    ALTER TABLE checkins       ENABLE ROW LEVEL SECURITY;
    ALTER TABLE work_orders    ENABLE ROW LEVEL SECURITY;
    ALTER TABLE services       ENABLE ROW LEVEL SECURITY;
    ALTER TABLE parts          ENABLE ROW LEVEL SECURITY;
    ALTER TABLE order_services ENABLE ROW LEVEL SECURITY;
    ALTER TABLE order_parts    ENABLE ROW LEVEL SECURITY;
    ALTER TABLE invoices       ENABLE ROW LEVEL SECURITY;
    ALTER TABLE payments       ENABLE ROW LEVEL SECURITY;
    ALTER TABLE warranty_cases ENABLE ROW LEVEL SECURITY;
    ALTER TABLE stock_moves    ENABLE ROW LEVEL SECURITY;
    ALTER TABLE audit_log      ENABLE ROW LEVEL SECURITY;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117113005_Initial') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261117113005_Initial', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117122002_SoftDelete') THEN
    ALTER TABLE users ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117122002_SoftDelete') THEN
    ALTER TABLE services ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117122002_SoftDelete') THEN
    ALTER TABLE parts ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117122002_SoftDelete') THEN
    ALTER TABLE clients ADD deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117122002_SoftDelete') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261117122002_SoftDelete', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117125355_RefreshTokens') THEN
    CREATE TABLE refresh_tokens (
        refresh_token_id integer GENERATED ALWAYS AS IDENTITY,
        user_id integer NOT NULL,
        token_hash character varying(64) NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT pk_refresh_tokens PRIMARY KEY (refresh_token_id),
        CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117125355_RefreshTokens') THEN
    CREATE UNIQUE INDEX ix_refresh_tokens_token_hash ON refresh_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117125355_RefreshTokens') THEN
    CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117125355_RefreshTokens') THEN
    ALTER TABLE refresh_tokens ENABLE ROW LEVEL SECURITY;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117125355_RefreshTokens') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261117125355_RefreshTokens', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117133418_StatsViews') THEN
    -- сводные цифры для главной страницы панели
    CREATE OR REPLACE VIEW v_stats_summary AS
    SELECT (SELECT count(*) FROM clients WHERE deleted_at IS NULL)                        AS clients,
           (SELECT count(*) FROM users WHERE deleted_at IS NULL)                          AS users,
           (SELECT count(*) FROM work_orders WHERE status NOT IN ('closed', 'cancelled')) AS active_orders,
           (SELECT coalesce(sum(i.amount), 0) FROM invoices i WHERE i.status <> 'cancelled')
         - (SELECT coalesce(sum(CASE WHEN p.is_refund THEN -p.amount ELSE p.amount END), 0)
            FROM payments p JOIN invoices i ON i.invoice_id = p.invoice_id
            WHERE i.status <> 'cancelled')                                               AS total_debt;

    -- сколько заказ-нарядов в каждом статусе
    CREATE OR REPLACE VIEW v_work_order_status_counts AS
    SELECT status, count(*) AS orders
    FROM work_orders
    GROUP BY status;

    -- выручка по месяцам: оплаты и возвраты отдельно
    CREATE OR REPLACE VIEW v_revenue_by_month AS
    SELECT date_trunc('month', paid_at)::date                    AS month,
           coalesce(sum(amount) FILTER (WHERE NOT is_refund), 0) AS income,
           coalesce(sum(amount) FILTER (WHERE is_refund), 0)     AS refunds
    FROM payments
    GROUP BY 1;

    -- запчасти ниже минимального запаса (как v_parts_below_min, но без функций)
    CREATE OR REPLACE VIEW v_stats_parts_below_min AS
    SELECT p.part_id,
           p.article,
           p.name,
           p.min_stock,
           coalesce(sum(CASE WHEN m.move_type IN ('receipt', 'return')   THEN m.quantity
                             WHEN m.move_type IN ('issue', 'write_off') THEN -m.quantity
                             ELSE 0 END), 0) AS on_hand
    FROM parts p
    LEFT JOIN stock_moves m ON m.part_id = p.part_id
    WHERE p.deleted_at IS NULL
    GROUP BY p.part_id, p.article, p.name, p.min_stock
    HAVING coalesce(sum(CASE WHEN m.move_type IN ('receipt', 'return')   THEN m.quantity
                             WHEN m.move_type IN ('issue', 'write_off') THEN -m.quantity
                             ELSE 0 END), 0) < p.min_stock;

    -- В Supabase у ролей anon и authenticated по умолчанию есть права на всё в public.
    -- Представления обходят RLS, поэтому у этих ролей права забираю (локально таких ролей нет)
    DO $$
    DECLARE r text;
    BEGIN
        FOREACH r IN ARRAY ARRAY['anon', 'authenticated'] LOOP
            IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
                EXECUTE format('REVOKE ALL ON ALL TABLES IN SCHEMA public FROM %I', r);
            END IF;
        END LOOP;
    END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117133418_StatsViews') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261117133418_StatsViews', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117153531_UserBlocking') THEN
    ALTER TABLE users ADD block_reason character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117153531_UserBlocking') THEN
    ALTER TABLE users ADD blocked_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117153531_UserBlocking') THEN
    ALTER TABLE users ADD is_blocked boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117153531_UserBlocking') THEN
    -- заблокирован ⇔ есть дата блокировки
    ALTER TABLE users ADD CONSTRAINT ck_users_blocked CHECK (is_blocked = (blocked_at IS NOT NULL));

    ALTER TABLE audit_log DROP CONSTRAINT ck_audit_action;
    ALTER TABLE audit_log ADD CONSTRAINT ck_audit_action CHECK (action IN (
        'INSERT', 'UPDATE', 'DELETE', 'LOGIN', 'LOGOUT', 'EXPORT', 'IMPORT', 'BACKUP', 'RESTORE',
        'LOGIN_FAILED', 'LOGIN_BLOCKED', 'UNAUTHORIZED', 'ACCESS_DENIED', 'NOT_FOUND', 'BLOCK', 'UNBLOCK'));

    -- журнал фильтруют по дате и действию, и по ним же считается статистика
    CREATE INDEX IF NOT EXISTS ix_audit_created ON audit_log (created_at);
    CREATE INDEX IF NOT EXISTS ix_audit_action  ON audit_log (action, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117153531_UserBlocking') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261117153531_UserBlocking', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117153612_SecurityStats') THEN
    -- пользователи: всего, активные (входили за 30 дней и не заблокированы), заблокированные, в корзине
    CREATE OR REPLACE VIEW v_stats_users AS
    SELECT count(*) FILTER (WHERE u.deleted_at IS NULL)                     AS total,
           count(*) FILTER (WHERE u.deleted_at IS NULL AND NOT u.is_blocked
                              AND EXISTS (SELECT 1 FROM audit_log a
                                          WHERE a.user_id = u.user_id AND a.action = 'LOGIN'
                                            AND a.created_at > now() - interval '30 days')) AS active,
           count(*) FILTER (WHERE u.deleted_at IS NULL AND u.is_blocked)    AS blocked,
           count(*) FILTER (WHERE u.deleted_at IS NOT NULL)                 AS deleted
    FROM users u;

    -- сколько пользователей в каждой роли
    CREATE OR REPLACE VIEW v_stats_users_by_role AS
    SELECT r.role_id, r.name AS role, count(u.user_id) AS users
    FROM roles r
    LEFT JOIN users u ON u.role_id = r.role_id AND u.deleted_at IS NULL
    GROUP BY r.role_id, r.name;

    -- регистрации (новые учётные записи) по месяцам
    CREATE OR REPLACE VIEW v_stats_registrations_by_month AS
    SELECT date_trunc('month', created_at)::date AS month, count(*) AS users
    FROM users
    GROUP BY 1;

    -- безопасность по дням (по Москве): входы, неудачные входы, попытки без прав, 404
    CREATE OR REPLACE VIEW v_stats_security_by_day AS
    SELECT (created_at AT TIME ZONE 'Europe/Moscow')::date                       AS day,
           count(*) FILTER (WHERE action = 'LOGIN')                              AS logins,
           count(*) FILTER (WHERE action IN ('LOGIN_FAILED', 'LOGIN_BLOCKED'))   AS failed,
           count(*) FILTER (WHERE action IN ('UNAUTHORIZED', 'ACCESS_DENIED'))   AS denied,
           count(*) FILTER (WHERE action = 'NOT_FOUND')                          AS not_found
    FROM audit_log
    WHERE action IN ('LOGIN', 'LOGIN_FAILED', 'LOGIN_BLOCKED', 'UNAUTHORIZED', 'ACCESS_DENIED', 'NOT_FOUND')
    GROUP BY 1;

    -- IP, с которых за 30 дней больше всего подозрительных запросов
    CREATE OR REPLACE VIEW v_stats_suspicious_ips AS
    SELECT ip_address AS ip, count(*) AS attempts, max(created_at) AS last_at
    FROM audit_log
    WHERE action IN ('LOGIN_FAILED', 'LOGIN_BLOCKED', 'UNAUTHORIZED', 'ACCESS_DENIED', 'NOT_FOUND')
      AND created_at > now() - interval '30 days'
      AND ip_address IS NOT NULL
    GROUP BY ip_address;

    -- новые клиенты и заказ-наряды по месяцам
    CREATE OR REPLACE VIEW v_stats_new_by_month AS
    SELECT month, sum(clients) AS clients, sum(orders) AS orders
    FROM (SELECT date_trunc('month', created_at)::date AS month, 1 AS clients, 0 AS orders FROM clients
          UNION ALL
          SELECT date_trunc('month', opened_at)::date, 0, 1 FROM work_orders) x
    GROUP BY month;

    -- среднее время ремонта (от открытия до закрытия заказ-наряда) по месяцу закрытия, в днях
    CREATE OR REPLACE VIEW v_stats_repair_time_by_month AS
    SELECT date_trunc('month', closed_at)::date                              AS month,
           count(*)                                                          AS closed,
           round(avg(extract(epoch FROM closed_at - opened_at) / 86400)::numeric, 1) AS avg_days
    FROM work_orders
    WHERE status = 'closed'
    GROUP BY 1;

    -- работы: сколько раз согласовали и на какую сумму (формула как в смете)
    CREATE OR REPLACE VIEW v_stats_top_services AS
    SELECT s.service_id, s.name, count(*) AS times,
           round(sum(s.norm_hours * s.rate * (1 + os.discount_markup / 100)), 2) AS revenue
    FROM order_services os
    JOIN services s ON s.service_id = os.service_id
    WHERE os.approval_status = 'approved'
    GROUP BY s.service_id, s.name;

    -- запчасти: сколько выдано в ремонт за вычетом возвратов
    CREATE OR REPLACE VIEW v_stats_top_parts AS
    SELECT p.part_id, p.article, p.name, p.unit,
           sum(CASE WHEN m.move_type = 'issue' THEN m.quantity ELSE -m.quantity END) AS used
    FROM stock_moves m
    JOIN parts p ON p.part_id = m.part_id
    WHERE m.move_type IN ('issue', 'return')
    GROUP BY p.part_id, p.article, p.name, p.unit;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117153612_SecurityStats') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261117153612_SecurityStats', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117165633_ClientAccounts') THEN
    ALTER TABLE clients ADD user_id integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117165633_ClientAccounts') THEN
    CREATE UNIQUE INDEX ix_clients_user_id ON clients (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117165633_ClientAccounts') THEN
    ALTER TABLE clients ADD CONSTRAINT fk_clients_user FOREIGN KEY (user_id) REFERENCES users (user_id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117165633_ClientAccounts') THEN
    UPDATE clients c
       SET user_id = u.user_id
      FROM users u
      JOIN roles r ON r.role_id = u.role_id AND r.code = 'client'
     WHERE c.user_id IS NULL
       AND lower(c.email) = u.email
       AND NOT EXISTS (SELECT 1 FROM clients x WHERE x.user_id = u.user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261117165633_ClientAccounts') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261117165633_ClientAccounts', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261123210046_HistoryRls') THEN
    ALTER TABLE "__EFMigrationsHistory" ENABLE ROW LEVEL SECURITY;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261123210046_HistoryRls') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261123210046_HistoryRls', '10.0.15');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261124063555_RepairRequests') THEN
    CREATE TABLE repair_requests (
        request_id integer GENERATED ALWAYS AS IDENTITY,
        device_type character varying(50) NOT NULL,
        device_model character varying(100) NOT NULL,
        problem text NOT NULL,
        delivery_method character varying(20) NOT NULL,
        branch character varying(100),
        address character varying(255),
        service_id integer,
        client_name character varying(150) NOT NULL,
        phone character varying(20) NOT NULL,
        email character varying(120),
        status character varying(20) NOT NULL DEFAULT 'new',
        comment character varying(255),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        processed_by integer,
        processed_at timestamp with time zone,
        CONSTRAINT pk_repair_requests PRIMARY KEY (request_id),
        CONSTRAINT ck_rr_delivery CHECK (delivery_method IN ('self', 'courier', 'master_visit')),
        CONSTRAINT ck_rr_phone CHECK (phone ~ '^\+7[0-9]{10}$'),
        CONSTRAINT ck_rr_place CHECK ((delivery_method = 'self' AND branch IS NOT NULL) OR (delivery_method <> 'self' AND address IS NOT NULL)),
        CONSTRAINT ck_rr_problem CHECK (length(problem) BETWEEN 5 AND 1000),
        CONSTRAINT ck_rr_processed CHECK (status IN ('new', 'processing') OR (processed_by IS NOT NULL AND processed_at IS NOT NULL)),
        CONSTRAINT ck_rr_status CHECK (status IN ('new', 'processing', 'done', 'rejected')),
        CONSTRAINT fk_rr_processed_by FOREIGN KEY (processed_by) REFERENCES users (user_id) ON DELETE SET NULL,
        CONSTRAINT fk_rr_service FOREIGN KEY (service_id) REFERENCES services (service_id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261124063555_RepairRequests') THEN
    CREATE INDEX ix_repair_requests_processed_by ON repair_requests (processed_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261124063555_RepairRequests') THEN
    CREATE INDEX ix_repair_requests_service_id ON repair_requests (service_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261124063555_RepairRequests') THEN
    CREATE INDEX ix_repair_requests_status_created_at ON repair_requests (status, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261124063555_RepairRequests') THEN

    ALTER TABLE repair_requests ENABLE ROW LEVEL SECURITY;



    CREATE TRIGGER audit_repair_requests AFTER INSERT OR UPDATE OR DELETE ON repair_requests

        FOR EACH ROW EXECUTE FUNCTION trg_audit('request_id');

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261124063555_RepairRequests') THEN

    DO $$

    BEGIN

        IF EXISTS (SELECT 1 FROM services) THEN

            UPDATE services SET name = 'Замена подшипников барабана стиральной машины', category = 'Крупная бытовая техника' WHERE name = 'Замена подшипников барабана' AND category = 'Стиральные машины';
            UPDATE services SET name = 'Замена ТЭНа стиральной машины', category = 'Крупная бытовая техника' WHERE name = 'Замена ТЭНа' AND category = 'Стиральные машины';
            UPDATE services SET name = 'Замена компрессора холодильника', category = 'Крупная бытовая техника' WHERE name = 'Замена компрессора' AND category = 'Холодильники';
            UPDATE services SET name = 'Заправка холодильника фреоном', category = 'Крупная бытовая техника' WHERE name = 'Заправка фреоном' AND category = 'Холодильники';
            UPDATE services SET name = 'Замена LED-подсветки телевизора', category = 'Крупная бытовая техника' WHERE name = 'Замена LED-подсветки матрицы' AND category = 'Телевизоры';
            UPDATE services SET name = 'Чистка системы охлаждения, замена пасты', category = 'Ноутбуки и ПК' WHERE name = 'Чистка системы охлаждения, замена пасты' AND category = 'Ноутбуки';
            UPDATE services SET name = 'Ремонт цепи питания материнской платы', category = 'Ноутбуки и ПК' WHERE name = 'Ремонт цепи питания материнской платы' AND category = 'Ноутбуки';

            INSERT INTO services (name, category, norm_hours, rate, warranty_months) VALUES

            ('Замена разъёма зарядки',                              'Смартфоны',               0.75, 1600.00, 6),
            ('Замена задней крышки',                                'Смартфоны',               0.50, 1600.00, 3),
            ('Замена основной камеры',                              'Смартфоны',               0.75, 1600.00, 3),
            ('Восстановление после попадания влаги',                'Смартфоны',               2.00, 1600.00, 1),
            ('Прошивка и восстановление ПО',                        'Смартфоны',               0.50, 1600.00, 1),
            ('Замена матрицы ноутбука',                             'Ноутбуки и ПК',           1.00, 1400.00, 6),
            ('Замена клавиатуры ноутбука',                          'Ноутбуки и ПК',           1.00, 1400.00, 3),
            ('Установка SSD и перенос данных',                      'Ноутбуки и ПК',           1.00, 1400.00, 3),
            ('Замена блока питания ПК',                             'Ноутбуки и ПК',           0.50, 1400.00, 6),
            ('Сборка и настройка ПК',                               'Ноутбуки и ПК',           2.00, 1400.00, 3),
            ('Замена сливного насоса стиральной машины',            'Крупная бытовая техника', 1.00, 1800.00, 6),
            ('Замена циркуляционного насоса посудомоечной машины',  'Крупная бытовая техника', 1.50, 1800.00, 6),
            ('Замена уплотнителя двери холодильника',               'Крупная бытовая техника', 1.00, 2000.00, 3),
            ('Замена конфорки электроплиты',                        'Крупная бытовая техника', 1.00, 1800.00, 6),
            ('Замена магнетрона микроволновой печи',                'Мелкая бытовая техника',  1.00, 1200.00, 6),
            ('Замена двигателя пылесоса',                           'Мелкая бытовая техника',  1.00, 1200.00, 6),
            ('Чистка кофемашины от накипи, замена уплотнителей',    'Мелкая бытовая техника',  1.50, 1200.00, 3),
            ('Замена ТЭНа электрочайника',                          'Мелкая бытовая техника',  0.50, 1200.00, 3),
            ('Замена шнура питания',                                'Мелкая бытовая техника',  0.50, 1200.00, 3),
            ('Ремонт фена или утюга',                               'Мелкая бытовая техника',  0.75, 1200.00, 3)

            ON CONFLICT (name, category) DO NOTHING;

        END IF;

    END $$;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "migration_id" = '20261124063555_RepairRequests') THEN
    INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
    VALUES ('20261124063555_RepairRequests', '10.0.15');
    END IF;
END $EF$;
COMMIT;

