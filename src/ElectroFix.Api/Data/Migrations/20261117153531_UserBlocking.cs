using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectroFix.Api.Data.Migrations
{
    // Блокировка пользователей и новые действия в журнале аудита: неудачные входы,
    // попытки без прав, запросы на несуществующие адреса, блокировка/разблокировка
    public partial class UserBlocking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "block_reason",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "blocked_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_blocked",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                -- заблокирован ⇔ есть дата блокировки
                ALTER TABLE users ADD CONSTRAINT ck_users_blocked CHECK (is_blocked = (blocked_at IS NOT NULL));

                ALTER TABLE audit_log DROP CONSTRAINT ck_audit_action;
                ALTER TABLE audit_log ADD CONSTRAINT ck_audit_action CHECK (action IN (
                    'INSERT', 'UPDATE', 'DELETE', 'LOGIN', 'LOGOUT', 'EXPORT', 'IMPORT', 'BACKUP', 'RESTORE',
                    'LOGIN_FAILED', 'LOGIN_BLOCKED', 'UNAUTHORIZED', 'ACCESS_DENIED', 'NOT_FOUND', 'BLOCK', 'UNBLOCK'));

                -- журнал фильтруют по дате и действию, и по ним же считается статистика
                CREATE INDEX IF NOT EXISTS ix_audit_created ON audit_log (created_at);
                CREATE INDEX IF NOT EXISTS ix_audit_action  ON audit_log (action, created_at);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_audit_action;
                DROP INDEX IF EXISTS ix_audit_created;
                DELETE FROM audit_log WHERE action IN ('LOGIN_FAILED', 'LOGIN_BLOCKED', 'UNAUTHORIZED',
                                                       'ACCESS_DENIED', 'NOT_FOUND', 'BLOCK', 'UNBLOCK');
                ALTER TABLE audit_log DROP CONSTRAINT ck_audit_action;
                ALTER TABLE audit_log ADD CONSTRAINT ck_audit_action CHECK (action IN (
                    'INSERT', 'UPDATE', 'DELETE', 'LOGIN', 'LOGOUT', 'EXPORT', 'IMPORT', 'BACKUP', 'RESTORE'));
                ALTER TABLE users DROP CONSTRAINT ck_users_blocked;
                """);

            migrationBuilder.DropColumn(
                name: "block_reason",
                table: "users");

            migrationBuilder.DropColumn(
                name: "blocked_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_blocked",
                table: "users");
        }
    }
}
