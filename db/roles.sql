-- Роли базы данных (RBAC на уровне PostgreSQL).
--
-- Api подключается владельцем таблиц: ему доступно всё, а права пользователей
-- (admin, приёмщик, мастер и т. д.) проверяются в самом Api по JWT.
-- Сервис статистики ElectroFix.Stats работает под отдельной ролью
-- electrofix_stats: она может только читать представления статистики.
-- Таблицы этой роли не видны: у неё нет на них GRANT, а RLS без правил
-- закрыл бы их, даже если бы права были.
--
-- Как запустить:
--   локально: psql -h localhost -p 5433 -U postgres -d electrofix_dev -f db/roles.sql
--   Supabase: SQL Editor → вставить скрипт → заменить пароль → Run
-- Скрипт можно запускать повторно. Запускать после того, как Api накатил миграции.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'electrofix_stats') THEN
        CREATE ROLE electrofix_stats LOGIN;
    END IF;
END $$;

-- настоящий пароль вписываю только в SQL Editor, в git он не попадает
ALTER ROLE electrofix_stats PASSWORD 'ЗАМЕНИ_НА_СВОЙ_ПАРОЛЬ';

-- никаких прав «на всякий случай»: сначала всё забираю, потом выдаю нужное
REVOKE ALL ON ALL TABLES IN SCHEMA public FROM electrofix_stats;
GRANT USAGE ON SCHEMA public TO electrofix_stats;
GRANT SELECT ON v_stats_summary,
                v_work_order_status_counts,
                v_revenue_by_month,
                v_master_workload,
                v_stats_parts_below_min,
                v_stats_users,
                v_stats_users_by_role,
                v_stats_registrations_by_month,
                v_stats_security_by_day,
                v_stats_suspicious_ips,
                v_stats_new_by_month,
                v_stats_repair_time_by_month,
                v_stats_top_services,
                v_stats_top_parts
      TO electrofix_stats;
