using ElectroFix.Admin.Infrastructure;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

// Админ-панель. Своей базы у неё нет: все данные берёт и меняет через Api по HTTP,
// а статистику для графиков — у сервиса ElectroFix.Stats
var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (port != null)
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// адреса сервисов: локально из appsettings.Development.json, на Render — Api__BaseUrl и Stats__BaseUrl
var apiUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Не задан Api:BaseUrl");
var statsUrl = builder.Configuration["Stats:BaseUrl"] ?? throw new InvalidOperationException("Не задан Stats:BaseUrl");

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TokenHandler>();
// «auth» — без обработчика токенов, через него идёт только /api/auth/refresh
builder.Services.AddHttpClient("auth", c => c.BaseAddress = new Uri(apiUrl));
builder.Services.AddHttpClient<ApiClient>(c =>
{
    c.BaseAddress = new Uri(apiUrl);
    // Render будит заснувший сервис около минуты
    c.Timeout = TimeSpan.FromSeconds(90);
}).AddHttpMessageHandler<TokenHandler>();
builder.Services.AddHttpClient<StatsClient>(c =>
{
    c.BaseAddress = new Uri(statsUrl);
    c.Timeout = TimeSpan.FromSeconds(90);
}).AddHttpMessageHandler<TokenHandler>();

// вход через cookie: в ней лежат роль из ответа Api и сами токены
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Denied";
        o.Cookie.Name = "ElectroFix.Admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        // на Render сайт работает по https, локально по http
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });

// политика на всё приложение: только роль admin (страницы входа помечены [AllowAnonymous])
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .RequireRole(RoleCodes.Admin)
    .Build());

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add<ApiErrorFilter>();
    // свои тексты ошибок разбора вместо английских «The value '' is invalid»
    var m = o.ModelBindingMessageProvider;
    m.SetValueMustBeANumberAccessor(_ => "Введите число");
    m.SetValueMustNotBeNullAccessor(_ => "Заполните поле");
    m.SetAttemptedValueIsInvalidAccessor((value, _) => $"Значение «{value}» не подходит");
    m.SetValueIsInvalidAccessor(value => $"Значение «{value}» не подходит");
    m.SetMissingBindRequiredValueAccessor(_ => "Заполните поле");
    m.SetMissingKeyOrValueAccessor(() => "Заполните поле");
    m.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Значение «{value}» не подходит");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// статика до авторизации, чтобы стили были видны и на странице входа
app.UseStaticFiles();
app.UseRouting();
app.UseRequestLocalization("ru-RU");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
