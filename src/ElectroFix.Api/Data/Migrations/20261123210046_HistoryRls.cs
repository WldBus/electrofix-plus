using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ElectroFix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HistoryRls : Migration
    {
        // Служебную таблицу со списком миграций EF создаёт сам, и мой 03_rls.sql её не задел.
        // Нашёл это restore-тестом: в Supabase она была единственной таблицей без RLS
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"__EFMigrationsHistory\" ENABLE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"__EFMigrationsHistory\" DISABLE ROW LEVEL SECURITY;");
        }
    }
}
