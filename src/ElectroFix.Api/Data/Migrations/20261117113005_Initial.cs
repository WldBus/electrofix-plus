using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectroFix.Api.Data.Migrations
{
    // Первая миграция. EF сгенерировал CREATE TABLE сам, но я заменил их на свои
    // SQL-скрипты из курсовой: в них все CHECK-ограничения, функции, представления,
    // процедуры и триггеры, которые EF описать не умеет. Снимок модели
    // (AppDbContextModelSnapshot) оставил, от него считаются следующие миграции.
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(SqlScripts.Read("01_tables.sql"));
            migrationBuilder.Sql(SqlScripts.Read("02_logic.sql"));
            migrationBuilder.Sql(SqlScripts.Read("03_rls.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // CASCADE сносит вместе с таблицами представления и триггеры
            migrationBuilder.Sql("""
                DROP TABLE IF EXISTS audit_log, stock_moves, warranty_cases, payments, invoices,
                    order_parts, order_services, work_orders, checkins, parts, services,
                    devices, clients, users, roles CASCADE;
                DROP PROCEDURE IF EXISTS sp_client_add, sp_client_update, sp_client_delete,
                    sp_open_work_order, sp_assign_master, sp_issue_part, sp_register_payment,
                    sp_close_work_order;
                DROP FUNCTION IF EXISTS fn_app_user_id, fn_user_role, fn_work_order_total,
                    fn_stock_on_hand, fn_stock_available, fn_invoice_paid, trg_work_orders_guard,
                    trg_stock_moves_guard, trg_immutable, trg_payments_guard, trg_audit;
                """);
        }
    }
}
