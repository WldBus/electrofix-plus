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
