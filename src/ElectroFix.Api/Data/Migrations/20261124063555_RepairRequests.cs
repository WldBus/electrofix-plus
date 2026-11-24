using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ElectroFix.Api.Data.Migrations
{
    // Заявки на ремонт с сайта без регистрации и прайс по 4 группам техники.
    // Таблицу закрываю RLS и вешаю на неё тот же триггер аудита, что и на остальные
    public partial class RepairRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "repair_requests",
                columns: table => new
                {
                    request_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    device_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    device_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    problem = table.Column<string>(type: "text", nullable: false),
                    delivery_method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    branch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    service_id = table.Column<int>(type: "integer", nullable: true),
                    client_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "new"),
                    comment = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    processed_by = table.Column<int>(type: "integer", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repair_requests", x => x.request_id);
                    table.CheckConstraint("ck_rr_delivery", "delivery_method IN ('self', 'courier', 'master_visit')");
                    table.CheckConstraint("ck_rr_phone", "phone ~ '^\\+7[0-9]{10}$'");
                    table.CheckConstraint("ck_rr_place", "(delivery_method = 'self' AND branch IS NOT NULL) OR (delivery_method <> 'self' AND address IS NOT NULL)");
                    table.CheckConstraint("ck_rr_problem", "length(problem) BETWEEN 5 AND 1000");
                    table.CheckConstraint("ck_rr_processed", "status IN ('new', 'processing') OR (processed_by IS NOT NULL AND processed_at IS NOT NULL)");
                    table.CheckConstraint("ck_rr_status", "status IN ('new', 'processing', 'done', 'rejected')");
                    table.ForeignKey(
                        name: "fk_rr_processed_by",
                        column: x => x.processed_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_rr_service",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "service_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_repair_requests_processed_by",
                table: "repair_requests",
                column: "processed_by");

            migrationBuilder.CreateIndex(
                name: "ix_repair_requests_service_id",
                table: "repair_requests",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "ix_repair_requests_status_created_at",
                table: "repair_requests",
                columns: new[] { "status", "created_at" });

            migrationBuilder.Sql("""
                ALTER TABLE repair_requests ENABLE ROW LEVEL SECURITY;

                CREATE TRIGGER audit_repair_requests AFTER INSERT OR UPDATE OR DELETE ON repair_requests
                    FOR EACH ROW EXECUTE FUNCTION trg_audit('request_id');
                """);

            // Прайс по 4 группам из задания. Только для уже заполненной базы (локальная, Supabase):
            // в пустую базу работы зальёт seed.sql, и номера работ в тестовых данных не съедут
            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM services) THEN
                {PriceSql.Regroup}
                        INSERT INTO services (name, category, norm_hours, rate, warranty_months) VALUES
                {PriceSql.NewServices}
                        ON CONFLICT (name, category) DO NOTHING;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // новые работы, которые ещё нигде не использованы, убираю, старым возвращаю категории
            migrationBuilder.Sql($"""
                DELETE FROM services s
                 WHERE (s.name, s.category) IN (SELECT name, category FROM (VALUES
                {PriceSql.NewServices}) AS v(name, category, h, r, w))
                   AND NOT EXISTS (SELECT 1 FROM order_services os WHERE os.service_id = s.service_id);
                {PriceSql.Ungroup}
                """);

            migrationBuilder.DropTable(
                name: "repair_requests");
        }
    }
}
