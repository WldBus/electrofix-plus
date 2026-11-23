using System.Text;

using ElectroFix.Shared;
using ElectroFix.Stats;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

using Npgsql;

// Сервис статистики — второй сервис рядом с основным Api (ядром).
// Он ничего не меняет в базе: читает только представления v_stats_* под ролью
// electrofix_stats (db/roles.sql) и отдаёт готовые цифры для графиков админки.
var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (port != null)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// своя строка подключения под ролью только для чтения: ConnectionStrings__Stats
var connectionString = builder.Configuration.GetConnectionString("Stats");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Не задан ConnectionStrings:Stats. См. README, раздел про секреты");
}
// GSS (Kerberos) выключаю, как и в Api: иначе в Docker в логе ошибка про libgssapi
builder.Services.AddSingleton(NpgsqlDataSource.Create(
    new NpgsqlConnectionStringBuilder(connectionString) { GssEncryptionMode = GssEncryptionMode.Disable }));
builder.Services.AddScoped<StatsRepository>();

// токен выдаёт основной Api, тут я его только проверяю тем же ключом Jwt__Key
var jwtKey = builder.Configuration["Jwt:Key"] ?? "";
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("Не задан Jwt:Key (тот же, что у Api)");
}
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ElectroFix",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ElectroFix",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = "role",
            NameClaimType = "name",
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// На Render перед приложением стоит прокси: к нему запрос приходит по HTTPS, а ко мне уже по HTTP.
// Беру настоящую схему и IP из X-Forwarded-*, иначе cookie Secure и редиректы ломаются.
// Адресов прокси Render я не знаю, поэтому списки доверенных очищаю (снаружи в контейнер не попасть)
var forwarded = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwarded.KnownIPNetworks.Clear();
forwarded.KnownProxies.Clear();
app.UseForwardedHeaders(forwarded);

app.UseAuthentication();
app.UseAuthorization();

// проверка, что сервис жив и база отвечает
app.MapGet("/api/health", async (StatsRepository stats) =>
    await stats.CanConnectAsync()
        ? Results.Ok(new { status = "ok", database = "ok" })
        : Results.Json(new { status = "degraded", database = "unavailable" }, statusCode: 503));

// статистика нужна только админке
app.MapGet("/api/stats/dashboard", async (StatsRepository stats, int? months) =>
        Results.Ok(await stats.DashboardAsync(Math.Clamp(months ?? 12, 1, 36))))
    .RequireAuthorization(p => p.RequireRole(RoleCodes.Admin));

app.Run();

public partial class Program;
