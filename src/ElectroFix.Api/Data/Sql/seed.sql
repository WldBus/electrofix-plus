-- Тестовые данные (из БД/ElectroFix_DB.sql, раздел 3). Загружает DbSeeder, если база пустая.
-- Пароль у всех тестовых пользователей: ElectroFix2026! (bcrypt-хэш)


-- 3.0 Роли
INSERT INTO roles (code, name) VALUES
 ('admin',        'Администратор БД'),    -- 1
 ('receptionist', 'Мастер-приёмщик'),     -- 2
 ('purchaser',    'Закупщик / Логист'),   -- 3
 ('master',       'Мастер'),              -- 4
 ('client',       'Клиент');              -- 5

-- 3.1 Пользователи
INSERT INTO users (role_id, full_name, email, phone, password_hash, settings) VALUES
 (1, 'Иванов Алексей Андреевич',  'admin@electrofix.ru',     '+79001110001', '$2b$12$I7.AtmSdcwjZBITFBixu2u.mpGdTGUyDRSMSymuVw8HJkZFzSQPRO', '{"theme":"dark","locale":"ru-RU"}'),
 (2, 'Петрова Мария Сергеевна',   'petrova@electrofix.ru',   '+79001110002', '$2b$12$I7.AtmSdcwjZBITFBixu2u.mpGdTGUyDRSMSymuVw8HJkZFzSQPRO', '{"theme":"light","locale":"ru-RU"}'),
 (2, 'Смирнов Олег Викторович',   'smirnov@electrofix.ru',   '+79001110003', '$2b$12$I7.AtmSdcwjZBITFBixu2u.mpGdTGUyDRSMSymuVw8HJkZFzSQPRO', '{"theme":"light","locale":"ru-RU"}'),
 (3, 'Кузнецов Дмитрий Игоревич', 'kuznetsov@electrofix.ru', '+79001110004', '$2b$12$I7.AtmSdcwjZBITFBixu2u.mpGdTGUyDRSMSymuVw8HJkZFzSQPRO', '{"theme":"light","locale":"ru-RU"}'),
 (4, 'Соколов Игорь Павлович',    'sokolov@electrofix.ru',   '+79001110005', '$2b$12$I7.AtmSdcwjZBITFBixu2u.mpGdTGUyDRSMSymuVw8HJkZFzSQPRO', '{"theme":"dark","locale":"ru-RU"}'),
 (4, 'Попов Андрей Николаевич',   'popov@electrofix.ru',     '+79001110006', '$2b$12$I7.AtmSdcwjZBITFBixu2u.mpGdTGUyDRSMSymuVw8HJkZFzSQPRO', '{"theme":"light","locale":"ru-RU"}'),
 (4, 'Васильева Елена Олеговна',  'vasilyeva@electrofix.ru', '+79001110007', '$2b$12$I7.AtmSdcwjZBITFBixu2u.mpGdTGUyDRSMSymuVw8HJkZFzSQPRO', '{"theme":"light","locale":"ru-RU"}'),
 (5, 'Новиков Сергей Петрович',   'novikov.sp@mail.ru',      '+79161234567', '$2b$12$I7.AtmSdcwjZBITFBixu2u.mpGdTGUyDRSMSymuVw8HJkZFzSQPRO', '{"locale":"ru-RU"}'),
 (5, 'Морозова Анна Викторовна',  'morozova.av@yandex.ru',   '+79262345678', '$2b$12$I7.AtmSdcwjZBITFBixu2u.mpGdTGUyDRSMSymuVw8HJkZFzSQPRO', '{"locale":"ru-RU"}');

-- 3.2 Справочник работ
INSERT INTO services (name, category, norm_hours, rate, warranty_months) VALUES
 ('Диагностика',                             'Общие',             0.50, 1500.00,  0),  -- 1
 ('Замена подшипников барабана',             'Стиральные машины', 2.50, 1800.00,  6),  -- 2
 ('Замена ТЭНа',                             'Стиральные машины', 1.00, 1800.00,  6),  -- 3
 ('Замена компрессора',                      'Холодильники',      3.00, 2200.00, 12),  -- 4
 ('Заправка фреоном',                        'Холодильники',      1.00, 2000.00,  3),  -- 5
 ('Замена дисплейного модуля',               'Смартфоны',         1.00, 1600.00,  3),  -- 6
 ('Замена аккумулятора',                     'Смартфоны',         0.50, 1600.00,  6),  -- 7
 ('Чистка системы охлаждения, замена пасты', 'Ноутбуки',          1.50, 1400.00,  1),  -- 8
 ('Ремонт цепи питания материнской платы',   'Ноутбуки',          3.00, 2000.00,  3),  -- 9
 ('Замена LED-подсветки матрицы',            'Телевизоры',        2.00, 1700.00,  6),  -- 10
 ('Отключение от сети и изоляция узла',      'Общие',             0.50, 1500.00,  0);  -- 11

-- 3.3 Номенклатура запчастей
INSERT INTO parts (article, name, category, unit, min_stock) VALUES
 ('SKF-6205-2Z',    'Подшипник 6205-2Z SKF',                'Подшипники',             'шт',    10),  -- 1
 ('SEAL-35x62x10',  'Сальник барабана 35x62x10',            'Уплотнители',            'шт',    10),  -- 2
 ('TEN-1950W',      'ТЭН 1950 Вт для стиральной машины',    'ТЭНы',                   'шт',     3),  -- 3
 ('CMP-LG-NT',      'Компрессор LG инверторный',            'Компрессоры',            'шт',     2),  -- 4
 ('R600A-100',      'Хладагент R600a, баллон 100 г',        'Расходники',             'шт',     5),  -- 5
 ('DSP-SM-A546',    'Дисплейный модуль Samsung Galaxy A54', 'Дисплеи',                'шт',     2),  -- 6
 ('BAT-IP13',       'Аккумулятор для iPhone 13',            'Аккумуляторы',           'шт',     3),  -- 7
 ('TP-MX4-4G',      'Термопаста Arctic MX-4, 4 г',          'Расходники',             'шт',     5),  -- 8
 ('LED-TV55-KIT',   'Комплект LED-подсветки 55"',           'Подсветка',              'компл',  1),  -- 9
 ('MOSFET-AON6414', 'MOSFET AON6414',                       'Электронные компоненты', 'шт',    20);  -- 10

-- 3.4 Клиенты
INSERT INTO clients (client_type, display_name, inn, address, phone, email) VALUES
 ('individual',         'Новиков Сергей Петрович',  NULL,           'г. Москва, ул. Лесная, д. 12, кв. 45',        '+79161234567', 'novikov.sp@mail.ru'),
 ('individual',         'Морозова Анна Викторовна', NULL,           'г. Москва, Ленинский пр-т, д. 80, кв. 7',     '+79262345678', 'morozova.av@yandex.ru'),
 ('legal_entity',       'ООО «ТехноСтрой»',         '7701234567',   'г. Москва, ул. Промышленная, д. 5, оф. 301',  '+74951234567', 'office@technostroy.ru'),
 ('management_company', 'УК «Уютный дом»',          '7723456781',   'г. Москва, ул. Судакова, д. 18',              '+74957654321', 'info@uyutdom.ru'),
 ('individual',         'Лебедев Павел Андреевич',  '771234567890', 'г. Москва, ул. Верхние Поля, д. 3, кв. 101', '+79033456789', NULL);

-- 3.5 Устройства
INSERT INTO devices (client_id, type, brand, model, manufacture_year, serial_number, inventory_number, runtime_hours) VALUES
 (1, 'Стиральная машина', 'Bosch',   'WAN28281OE',      2019, 'FD9803-00157',   NULL,          2600),  -- 1
 (1, 'Смартфон',          'Apple',   'iPhone 13',       2021, 'F2LXK9QHN736',   NULL,             0),  -- 2
 (2, 'Смартфон',          'Samsung', 'Galaxy A54',      2023, 'R5CW31ABCDE',    NULL,             0),  -- 3
 (3, 'Ноутбук',           'Lenovo',  'ThinkPad E15 G4', 2022, 'PF3ABCD1',       'ТС-ИТ-00412', 5200),  -- 4
 (3, 'Ноутбук',           'HP',      'ProBook 450 G9',  2023, '5CD2345XYZ',     'ТС-ИТ-00587', 3100),  -- 5
 (4, 'Холодильник',       'LG',      'GA-B509CQSL',     2018, '812KRAB0Q123',   'УК-ХО-0015', 51000),  -- 6
 (5, 'Телевизор',         'Samsung', 'UE55AU7100U',     2021, '0B7Y3CAR500123', NULL,          9800),  -- 7
 (2, 'Стиральная машина', 'Indesit', 'IWSB 5105',       2016, 'IND5105-44821',  NULL,          4100);  -- 8

-- 3.6 Записи / приёмка
INSERT INTO checkins (device_id, receptionist_id, branch, slot_start, slot_end,
                      services_requested, notes, photos, defects, condition_state,
                      runtime_at_checkin, priority, status) VALUES
 (1, 2, 'Москва, Тверская', '2026-09-01 10:00', '2026-09-01 10:30',
  'Диагностика; замена подшипников', 'Сильный гул при отжиме',
  '["/photos/ci1/front.jpg","/photos/ci1/drum.jpg"]', 'Люфт барабана, гул на отжиме',
  'Корпус без повреждений', 2600, 'normal', 'checked_in'),
 (3, 2, 'Москва, Тверская', '2026-09-08 12:00', '2026-09-08 12:20',
  'Замена дисплея', 'Разбит экран после падения', '["/photos/ci2/screen.jpg"]',
  'Трещины стекла, полосы на матрице', 'Потёртости корпуса', NULL, 'high', 'checked_in'),
 (4, 3, 'Москва, Люблино', '2026-09-10 09:00', '2026-09-10 09:30',
  'Не включается', 'Корпоративный ноутбук, инв. ТС-ИТ-00412', '["/photos/ci3/top.jpg"]',
  'Нет реакции на кнопку питания', 'Следы залития клавиатуры', 5200, 'urgent', 'checked_in'),
 (6, 3, 'Москва, Люблино', '2026-09-03 14:00', '2026-09-03 15:00',
  'Не морозит', 'Выезд + доставка в СЦ', '["/photos/ci4/back.jpg"]',
  'Компрессор не запускается, щелчки реле', 'Вмятина на боковой панели', 51000, 'high', 'checked_in'),
 (7, 2, 'Москва, Тверская', '2026-09-15 16:00', '2026-09-15 16:30',
  'Нет изображения', 'Звук есть, изображения нет', '["/photos/ci5/panel.jpg"]',
  'Нет подсветки', 'Без внешних повреждений', 9800, 'normal', 'checked_in'),
 (2, NULL, 'Москва, Тверская', '2026-09-24 11:00', '2026-09-24 11:30',
  'Замена аккумулятора', 'Запись из ЛК клиента', NULL, NULL, NULL, NULL, 'low', 'booked'),
 (8, 2, 'Москва, Тверская', '2026-09-25 10:00', '2026-09-25 10:30',
  'Не сливает воду', 'Подтверждено по телефону', NULL, NULL, NULL, NULL, 'normal', 'confirmed');

-- 3.7 Заказ-наряды
INSERT INTO work_orders (work_order_number, checkin_id, receptionist_id, master_id, status, opened_at, closed_at) VALUES
 ('ЗН-2026-00001', 1, 2, 5,    'closed',      '2026-09-01 10:30', '2026-09-04 18:00'),
 ('ЗН-2026-00002', 2, 2, 6,    'in_progress', '2026-09-08 12:20', NULL),
 ('ЗН-2026-00003', 3, 3, NULL, 'estimate',    '2026-09-10 09:30', NULL),
 ('ЗН-2026-00004', 4, 3, 5,    'ready',       '2026-09-03 15:00', NULL),
 ('ЗН-2026-00005', 5, 2, 7,    'diagnostics', '2026-09-15 16:30', NULL);

-- 3.8 Позиции нарядов: работы
INSERT INTO order_services (work_order_id, service_id, estimate_version, actual_time,
                            approval_status, discount_markup, is_emergency) VALUES
 (1, 1,  1, 0.50, 'approved',   0, false),
 (1, 2,  1, 2.80, 'approved',   0, false),
 (2, 1,  1, 0.40, 'approved',   0, false),
 (2, 6,  1, 0.00, 'approved',   0, false),
 (3, 1,  1, 0.00, 'rejected',   0, false),
 (3, 9,  1, 0.00, 'rejected',   0, false),
 (3, 1,  2, 0.00, 'pending',    0, false),
 (3, 9,  2, 0.00, 'pending',  -10, false),
 (3, 8,  2, 0.00, 'pending',  -10, false),
 (4, 11, 1, 0.50, 'approved',   0, true),
 (4, 1,  1, 0.50, 'approved',   0, false),
 (4, 4,  1, 3.50, 'approved',   0, false),
 (4, 5,  1, 1.00, 'approved',   0, false),
 (5, 1,  1, 0.00, 'draft',      0, false),
 (5, 10, 1, 0.00, 'draft',      0, false);

-- 3.9 Позиции нарядов: запчасти
INSERT INTO order_parts (work_order_id, part_id, quantity, price, batch, approval_status) VALUES
 (1, 1,  2,   850.00, 'П-2026-0101', 'approved'),
 (1, 2,  1,   450.00, 'П-2026-0101', 'approved'),
 (2, 6,  1,  8900.00, 'П-2026-0104', 'approved'),
 (3, 10, 2,   350.00, 'П-2026-0105', 'rejected'),
 (3, 8,  1,   690.00, 'П-2026-0105', 'pending'),
 (4, 4,  1, 18500.00, 'П-2026-0103', 'approved'),
 (4, 5,  1,   900.00, 'П-2026-0103', 'approved'),
 (5, 9,  1,  6200.00, 'П-2026-0106', 'draft');

-- 3.10 Движения склада
INSERT INTO stock_moves (part_id, work_order_id, move_type, quantity, batch, storage_cell, performed_by, moved_at) VALUES
 (1,  NULL, 'receipt',   20, 'П-2026-0101', 'A-01-01', 4, '2026-08-20 10:00'),
 (2,  NULL, 'receipt',   15, 'П-2026-0101', 'A-01-02', 4, '2026-08-20 10:05'),
 (3,  NULL, 'receipt',    4, 'П-2026-0102', 'A-02-01', 4, '2026-08-21 11:00'),
 (4,  NULL, 'receipt',    2, 'П-2026-0103', 'C-01-01', 4, '2026-08-22 09:30'),
 (5,  NULL, 'receipt',   10, 'П-2026-0103', 'C-01-02', 4, '2026-08-22 09:35'),
 (6,  NULL, 'receipt',    3, 'П-2026-0104', 'B-03-01', 4, '2026-08-25 14:00'),
 (7,  NULL, 'receipt',    5, 'П-2026-0104', 'B-03-02', 4, '2026-08-25 14:05'),
 (8,  NULL, 'receipt',   12, 'П-2026-0105', 'B-04-01', 4, '2026-08-26 12:00'),
 (10, NULL, 'receipt',   50, 'П-2026-0105', 'B-04-02', 4, '2026-08-26 12:10'),
 (9,  NULL, 'receipt',    1, 'П-2026-0106', 'D-01-01', 4, '2026-08-27 16:00'),
 (6,  NULL, 'receipt',    2, 'П-2026-0110', 'B-03-01', 4, '2026-09-10 15:00'),
 (8,  NULL, 'write_off',  1, 'П-2026-0105', NULL,      4, '2026-08-27 10:00'),
 (1,  1,    'reserve',    2, 'П-2026-0101', NULL,      4, '2026-09-02 09:00'),
 (2,  1,    'reserve',    1, 'П-2026-0101', NULL,      4, '2026-09-02 09:01'),
 (1,  1,    'issue',      2, 'П-2026-0101', NULL,      4, '2026-09-02 10:00'),
 (2,  1,    'issue',      1, 'П-2026-0101', NULL,      4, '2026-09-02 10:01'),
 (6,  2,    'reserve',    1, 'П-2026-0104', NULL,      4, '2026-09-09 10:00'),
 (6,  2,    'issue',      1, 'П-2026-0104', NULL,      4, '2026-09-09 11:00'),
 (4,  4,    'reserve',    1, 'П-2026-0103', NULL,      4, '2026-09-05 09:00'),
 (5,  4,    'reserve',    1, 'П-2026-0103', NULL,      4, '2026-09-05 09:01'),
 (4,  4,    'issue',      1, 'П-2026-0103', NULL,      4, '2026-09-05 10:00'),
 (5,  4,    'issue',      1, 'П-2026-0103', NULL,      4, '2026-09-05 10:01');

-- 3.11 Счета
INSERT INTO invoices (invoice_number, work_order_id, amount, price_list_ref, status, issued_at) VALUES
 ('СЧ-2026-00001', 1,  7400.00, 'ПЛ-2026-09', 'paid',           '2026-09-04 17:00'),
 ('СЧ-2026-00002', 4, 29500.00, 'ПЛ-2026-09', 'partially_paid', '2026-09-12 12:00');

-- 3.12 Платежи
INSERT INTO payments (invoice_id, amount, method, is_refund, paid_at) VALUES
 (1,  7400.00, 'cash',          false, '2026-09-04 18:05'),
 (2, 20000.00, 'bank_transfer', false, '2026-09-14 10:00');

-- 3.13 Гарантийные случаи
INSERT INTO warranty_cases (work_order_id, reason, inspection_notes, decision, settlement_amount, created_at) VALUES
 (1, 'Повторный гул барабана через 2 недели после ремонта',
  'Дефект сальника из партии П-2026-0101 (заводской брак), подшипники в норме',
  'free_repair', 450.00, '2026-09-18 11:00');

-- 3.14 Аудит-лог
INSERT INTO audit_log (user_id, action, object_table, object_id, before_data, after_data, ip_address, user_agent, created_at) VALUES
 (2, 'INSERT', 'work_orders', 1, NULL, '{"status":"new"}',                                      '10.10.1.5',   'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0',  '2026-09-01 10:30'),
 (8, 'UPDATE', 'order_services', 1, '{"approval_status":"pending"}', '{"approval_status":"approved"}', '95.24.10.33', 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_5) Safari/604.1', '2026-09-01 15:10'),
 (2, 'UPDATE', 'work_orders', 1, '{"status":"ready"}', '{"status":"closed"}',                   '10.10.1.5',   'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0',  '2026-09-04 18:00'),
 (2, 'EXPORT', 'invoices', 1, NULL, NULL,                                                       '10.10.1.5',   'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0',  '2026-09-04 17:02'),
 (4, 'IMPORT', 'parts', NULL, NULL, NULL,                                                       '10.10.1.7',   'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Firefox/130.0', '2026-09-19 14:20'),
 (1, 'BACKUP', 'database', NULL, NULL, NULL,                                                    '10.10.1.2',   'pg_dump 16.4',                                            '2026-09-20 03:00'),
 (1, 'LOGIN',  'users', 1, NULL, NULL,                                                          '10.10.1.2',   'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/128.0',  '2026-09-20 08:55');

