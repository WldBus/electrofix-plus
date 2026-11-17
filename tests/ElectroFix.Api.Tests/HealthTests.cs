using System.Net;
using System.Net.Http.Json;

using ElectroFix.Api.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElectroFix.Api.Tests;

public class HealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_ReturnsOk_WhenDatabaseIsUp()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ok", body!["status"]);
        Assert.Equal("ok", body["database"]);
    }

    [Fact]
    public async Task Startup_AppliesMigrationAndSeed()
    {
        // CreateClient запускает Api, а вместе с ним миграцию и сидер
        factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(5, await db.Roles.CountAsync());
        Assert.True(await db.Users.AnyAsync(u => u.Email == "admin@electrofix.ru"));
        Assert.True(await db.WorkOrders.AnyAsync());

        // проверяю, что сидер включил триггеры обратно
        var disabledTriggers = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_trigger WHERE NOT tgisinternal AND tgenabled = 'D'")
            .SingleAsync();
        Assert.Equal(0, disabledTriggers);
    }
}
