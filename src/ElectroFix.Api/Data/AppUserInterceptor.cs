using System.Data.Common;

using ElectroFix.Api.Common;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ElectroFix.Api.Data;

// Триггер аудита в базе берёт автора изменения из настройки app.user_id
// (функция fn_app_user_id). Поэтому при каждом открытии соединения
// записываю туда id того, кто сделал запрос. Без входа — пустая строка,
// чтобы соединение из пула не досталось следующему запросу с чужим id
public class AppUserInterceptor(IHttpContextAccessor accessor) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var userId = new CurrentUser(accessor).Id;
        var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.user_id', @id, false)";
        var p = command.CreateParameter();
        p.ParameterName = "id";
        p.Value = userId?.ToString() ?? "";
        command.Parameters.Add(p);
        return command;
    }
}
