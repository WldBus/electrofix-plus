using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;

namespace ElectroFix.Web.Infrastructure;

// Ограничение частоты для открытых форм главной (статус ремонта и заявка).
// Ставлю его здесь, а не только в Api: до Api все запросы доходят с одного IP — IP сайта,
// а тут виден настоящий IP посетителя (после UseForwardedHeaders)
public static class SiteLimits
{
    public const string PublicForms = "public-forms";

    public static void Add(RateLimiterOptions o)
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy(PublicForms, http => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        o.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
            await context.HttpContext.Response.WriteAsync(
                "Слишком много запросов с вашего адреса. Подождите минуту и вернитесь назад.", token);
        };
    }
}
