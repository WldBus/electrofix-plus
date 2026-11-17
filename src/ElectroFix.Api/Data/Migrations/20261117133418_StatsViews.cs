using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectroFix.Api.Data.Migrations
{
    // Представления для сервиса статистики (ElectroFix.Stats). Сервис ходит в базу
    // под отдельной ролью только для чтения, и права у неё только на эти представления
    // (см. db/roles.sql). Представление выполняется с правами владельца, поэтому
    // RLS на таблицах ему не мешает, а саму таблицу роль статистики не прочитает.
    // Функции fn_* внутри не вызываю: функция выполняется с правами того, кто читает,
    // и у роли статистики на ней был бы «нет доступа к таблице».
    public partial class StatsViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP VIEW IF EXISTS v_stats_parts_below_min;
                DROP VIEW IF EXISTS v_revenue_by_month;
                DROP VIEW IF EXISTS v_work_order_status_counts;
                DROP VIEW IF EXISTS v_stats_summary;
                """);
        }
    }
}
