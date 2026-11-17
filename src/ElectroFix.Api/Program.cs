using System.Security.Claims;
using System.Text.Json.Serialization.Metadata;
using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Api.Services;
using ElectroFix.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// на Render порт приходит в переменной PORT, локально берётся из launchSettings
var port = Environment.GetEnvironmentVariable("PORT");
if (port != null)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<AppUserInterceptor>();

// строка подключения: локально из user-secrets, на Render из переменной ConnectionStrings__Default
var connectionString = builder.Configuration.GetConnectionString("Default");
builder.Services.AddDbContext<AppDbContext>((sp, o) => o
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(sp.GetRequiredService<AppUserInterceptor>())
    // EF предупреждает, что фильтр корзины может спрятать сотрудника или работу из старого заказ-наряда.
    // Я это знаю: в истории везде пишу IgnoreQueryFilters()
    .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));

// ключ подписи JWT: локально из user-secrets, на Render из переменной Jwt__Key
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32 && !EF.IsDesignTime)
{
    throw new InvalidOperationException("Не задан Jwt:Key (нужно не меньше 32 символов). См. README, раздел про секреты");
}
builder.Services.AddSingleton(jwt);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // claims оставляю как есть: sub, name, role
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = jwt.SigningKey(),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        // 401 и 403 отдаю в том же формате, что и остальные ошибки
        o.Events = new JwtBearerEvents
        {
            // токен подписан верно, но пользователя могли заблокировать или удалить после выдачи.
            // Проверяю по базе на каждом запросе, чтобы блокировка срабатывала сразу, а не через 15 минут
            OnTokenValidated = async context =>
            {
                var id = UserIdOf(context.Principal);
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                if (id == null || !await db.Users.AnyAsync(u => u.UserId == id && !u.IsBlocked))
                {
                    context.Fail("Учётная запись заблокирована или удалена");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                // обычное истечение токена не пишу: панель сама обновит его по refresh-токену
                if (context.AuthenticateFailure is not SecurityTokenExpiredException)
                {
                    var log = context.HttpContext.RequestServices.GetRequiredService<SecurityLog>();
                    await log.WriteRequestAsync("UNAUTHORIZED", null, 401);
                }
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(
                    context.AuthenticateFailure?.Message == "Учётная запись заблокирована или удалена"
                        ? "Учётная запись заблокирована или удалена"
                        : "Нужно войти в систему (или токен истёк)"));
            },
            // вошедший пользователь полез туда, куда его роли нельзя (например, клиент в /api/users)
            OnForbidden = async context =>
            {
                var log = context.HttpContext.RequestServices.GetRequiredService<SecurityLog>();
                await log.WriteRequestAsync("ACCESS_DENIED", UserIdOf(context.HttpContext.User), 403);
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Вашей роли это действие недоступно"));
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<ClientsService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<RepairService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SecurityLog>();

// ФИО в ответах админу скрываю (см. PersonalDataJson)
builder.Services.AddOptions<Microsoft.AspNetCore.Mvc.JsonOptions>()
    .Configure<IHttpContextAccessor>((o, http) => o.JsonSerializerOptions.TypeInfoResolver =
        new DefaultJsonTypeInfoResolver { Modifiers = { PersonalDataJson.Modifier(http) } });

builder.Services.AddControllers()
    // ошибки DataAnnotations отдаю в своём формате с кодом 422
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = ValidationResponse.Create);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    // кнопка Authorize в Swagger: вставляешь accessToken из /api/auth/login
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "accessToken из ответа /api/auth/login",
    });
    o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

var app = builder.Build();

app.UseMiddleware<ErrorMiddleware>();

// при старте докатываю миграции и, если база пустая, заливаю тестовые данные
if (!string.IsNullOrEmpty(connectionString))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db);
}

// Swagger оставляю и в проде, это курсовая
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();

// запрос на несуществующий адрес Api: отвечаю в общем формате и пишу в журнал —
// так видно, если кто-то перебирает адреса
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == 404 && !context.Response.HasStarted && context.GetEndpoint() == null
        && context.Request.Path.StartsWithSegments("/api"))
    {
        await context.RequestServices.GetRequiredService<SecurityLog>()
            .WriteRequestAsync("NOT_FOUND", UserIdOf(context.User), 404);
        await context.Response.WriteAsJsonAsync(new ErrorResponse("Такого адреса в Api нет"));
    }
});

app.UseAuthorization();
app.MapControllers();

app.Run();

static int? UserIdOf(ClaimsPrincipal? user) =>
    int.TryParse(user?.FindFirst("sub")?.Value, out var id) ? id : null;

// нужно тестам (WebApplicationFactory<Program>)
public partial class Program;
