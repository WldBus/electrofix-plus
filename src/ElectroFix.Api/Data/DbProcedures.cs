using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace ElectroFix.Api.Data;

// Вызов хранимых процедур (CALL sp_...). Через EF их неудобно звать, поэтому напрямую
// через Npgsql на том же подключении, что и у контекста (с app.user_id для аудита)
public static class DbProcedures
{
    // INOUT-параметр процедура возвращает первой колонкой — его и отдаю
    public static async Task<T?> CallAsync<T>(this AppDbContext db, string sql, params (string Name, object? Value)[] args)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = new NpgsqlCommand(sql, connection);
            foreach (var (name, value) in args)
            {
                command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            }
            var result = await command.ExecuteScalarAsync();
            return result is T typed ? typed : default;
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
