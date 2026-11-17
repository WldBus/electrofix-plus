using System.Text.Encodings.Web;
using System.Text.Json;

using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;

namespace ElectroFix.Api.Services;

// Запись в audit_log того, что не ловят триггеры: входы, выходы, блокировки
// и подозрительные запросы (без входа, без прав, на несуществующий адрес).
// Админ видит это в журнале аудита и на графиках безопасности
public class SecurityLog(AppDbContext db, IHttpContextAccessor http)
{
    // кириллицу в JSON оставляю читаемой, без а
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public async Task WriteAsync(string action, int? userId, string table = "users", int? objectId = null, object? data = null)
    {
        var context = http.HttpContext;
        var userAgent = context?.Request.Headers.UserAgent.ToString();
        db.AuditLog.Add(new AuditEntry
        {
            UserId = userId,
            Action = action,
            ObjectTable = table,
            ObjectId = objectId,
            AfterData = data == null ? null : JsonSerializer.Serialize(data, Json),
            IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = userAgent is { Length: > 255 } ? userAgent[..255] : userAgent,
        });
        await db.SaveChangesAsync();
    }

    // запрос, который не пустили: 401, 403 или 404. Пишу метод и адрес без параметров
    public Task WriteRequestAsync(string action, int? userId, int status)
    {
        var request = http.HttpContext!.Request;
        return WriteAsync(action, userId, "http", null, new { method = request.Method, path = request.Path.Value, status });
    }
}
