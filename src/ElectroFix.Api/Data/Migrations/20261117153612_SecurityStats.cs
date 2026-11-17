using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectroFix.Api.Data.Migrations
{
    // Ещё представления для сервиса статистики: пользователи, безопасность,
    // клиенты и заказы, работы и запчасти. Правила те же, что в StatsViews:
    // без функций fn_*, права роли electrofix_stats выдаются в db/roles.sql
    public partial class SecurityStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP VIEW IF EXISTS v_stats_top_parts;
                DROP VIEW IF EXISTS v_stats_top_services;
                DROP VIEW IF EXISTS v_stats_repair_time_by_month;
                DROP VIEW IF EXISTS v_stats_new_by_month;
                DROP VIEW IF EXISTS v_stats_suspicious_ips;
                DROP VIEW IF EXISTS v_stats_security_by_day;
                DROP VIEW IF EXISTS v_stats_registrations_by_month;
                DROP VIEW IF EXISTS v_stats_users_by_role;
                DROP VIEW IF EXISTS v_stats_users;
                """);
        }
    }
}
