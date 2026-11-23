using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectroFix.Api.Data.Migrations
{
    // Связь «учётка на сайте — карточка клиента» для личного кабинета.
    // Учётки клиентов из тестовых данных сразу привязываю к карточкам с тем же email
    public partial class ClientAccounts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "user_id",
                table: "clients",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_user_id",
                table: "clients",
                column: "user_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_clients_user",
                table: "clients",
                column: "user_id",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql("""
                UPDATE clients c
                   SET user_id = u.user_id
                  FROM users u
                  JOIN roles r ON r.role_id = u.role_id AND r.code = 'client'
                 WHERE c.user_id IS NULL
                   AND lower(c.email) = u.email
                   AND NOT EXISTS (SELECT 1 FROM clients x WHERE x.user_id = u.user_id);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_clients_user",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "ix_clients_user_id",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "user_id",
                table: "clients");
        }
    }
}
