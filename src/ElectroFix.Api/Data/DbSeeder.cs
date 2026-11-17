using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Data;

public static class DbSeeder
{
    // таблицы, на которых висят мои триггеры
    private static readonly string[] TablesWithTriggers =
    [
        "users", "clients", "devices", "checkins", "work_orders", "order_services",
        "order_parts", "invoices", "payments", "warranty_cases", "stock_moves",
    ];

    public static void Seed(AppDbContext db)
    {
        // если роли уже есть, значит база заполнена, второй раз не лью
        if (db.Roles.Any())
        {
            return;
        }

        // тестовые данные описывают уже закрытые заказы, а триггеры не дают
        // «перепрыгнуть» статусы и пишут аудит. На время заливки выключаю их
        var disable = string.Join("\n", TablesWithTriggers.Select(t => $"ALTER TABLE {t} DISABLE TRIGGER USER;"));
        var enable = string.Join("\n", TablesWithTriggers.Select(t => $"ALTER TABLE {t} ENABLE TRIGGER USER;"));

        // всё одним скриптом в транзакции: либо зальётся целиком, либо ничего
        var script = $"BEGIN;\n{disable}\n{SqlScripts.Read("seed.sql")}\n{enable}\nCOMMIT;";

        // ExecuteSqlRaw не беру: в сиде есть фигурные скобки (json), их EF может принять за параметры
        var connection = db.Database.GetDbConnection();
        connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = script;
            command.ExecuteNonQuery();
        }
        finally
        {
            connection.Close();
        }
    }
}
