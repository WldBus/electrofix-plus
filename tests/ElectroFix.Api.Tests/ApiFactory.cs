using System.Net.Http.Headers;
using System.Net.Http.Json;

using ElectroFix.Shared;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Testcontainers.PostgreSql;

namespace ElectroFix.Api.Tests;

// Поднимаю для тестов настоящий PostgreSQL в Docker: процедуры и триггеры у меня
// на PL/pgSQL, на SQLite или InMemory их не проверить. Api при старте сам
// накатывает миграции и заливает тестовые данные, как в проде.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // у всех тестовых пользователей из seed.sql один пароль
    public const string SeedPassword = "ElectroFix2026!";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        // ключ только для тестов, настоящий лежит в user-secrets и на хостинге
        builder.UseSetting("Jwt:Key", "test-key-for-integration-tests-only-0123456789");
    }

    // клиент, который уже вошёл под указанным пользователем
    public async Task<HttpClient> ClientAsAsync(string email)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = email, Password = SeedPassword });
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
