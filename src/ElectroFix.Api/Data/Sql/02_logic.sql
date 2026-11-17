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

