using System.Net;
using System.Net.Http.Json;

using ElectroFix.Shared;

namespace ElectroFix.Api.Tests;

// замечания преподавателя: скрытые ФИО для админа, блокировка, события безопасности в журнале
public class SecurityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void MaskName_HidesPersonButNotCompany()
    {
        Assert.Equal("И****в А. А.", PersonalData.MaskName("Иванов Алексей Андреевич"));
        Assert.Equal("ООО «ТехноСтрой»", PersonalData.MaskName("ООО «ТехноСтрой»"));
        Assert.Equal("{\"full_name\": \"П*****а М. С.\"}", PersonalData.MaskJson("{\"full_name\": \"Петрова Мария Сергеевна\"}"));
    }

    [Fact]
    public async Task Admin_SeesMaskedNames_ExceptOwn_StaffSeesFull()
    {
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        var users = await admin.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?size=100");

        Assert.Equal("Иванов Алексей Андреевич", users!.Items.Single(u => u.Email == "admin@electrofix.ru").FullName);
        Assert.Equal("П*****а М. С.", users.Items.Single(u => u.Email == "petrova@electrofix.ru").FullName);
        // email админ видит, по нему и работает с учётками
        Assert.Contains(users.Items, u => u.Email == "novikov.sp@mail.ru");

        var receptionist = await factory.ClientAsAsync("petrova@electrofix.ru");
        var clients = await receptionist.GetFromJsonAsync<PagedResult<ClientDto>>("/api/clients?size=100");
        Assert.Contains(clients!.Items, c => c.DisplayName == "Новиков Сергей Петрович");
    }

    [Fact]
    public async Task Admin_SavesFormWithMaskedName_RealNameStays()
    {
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        var smirnov = await FindUserAsync(admin, "smirnov@electrofix.ru");

        var response = await admin.PutAsJsonAsync($"/api/users/{smirnov.UserId}", new UserSaveRequest
        {
            RoleId = smirnov.RoleId,
            FullName = smirnov.FullName, // пришло скрытым «С*****в О. В.»
            Email = smirnov.Email,
            Phone = "+79009998877",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // сам Смирнов видит своё настоящее ФИО
        var self = await factory.ClientAsAsync("smirnov@electrofix.ru");
        var me = await self.GetFromJsonAsync<UserDto>("/api/auth/me");
        Assert.Equal("Смирнов Олег Викторович", me!.FullName);
        Assert.Equal("+79009998877", me.Phone);
    }

    [Fact]
    public async Task Block_CutsSessionAtOnce_AndUnblockLetsBack()
    {
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        var popov = await factory.ClientAsAsync("popov@electrofix.ru");
        var id = (await FindUserAsync(admin, "popov@electrofix.ru")).UserId;

        var block = await admin.PostAsJsonAsync($"/api/users/{id}/block", new BlockRequest { Reason = "Уволен" });
        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);

        // старый access-токен ещё не истёк, но уже не принимается
        Assert.Equal(HttpStatusCode.Unauthorized, (await popov.GetAsync("/api/work-orders")).StatusCode);
        var login = await LoginAsync("popov@electrofix.ru", ApiFactory.SeedPassword);
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.True((await FindUserAsync(admin, "popov@electrofix.ru")).IsBlocked);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/users/{id}/unblock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("popov@electrofix.ru", ApiFactory.SeedPassword)).StatusCode);

        var audit = await AuditAsync(admin, "action=LOGIN_BLOCKED");
        Assert.Contains(audit, a => a.ObjectId == id);
        Assert.Contains(await AuditAsync(admin, "action=BLOCK"), a => a.ObjectId == id && a.AfterData!.Contains("Уволен"));
    }

    [Fact]
    public async Task Admin_CannotBlockSelf()
    {
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        var id = (await FindUserAsync(admin, "admin@electrofix.ru")).UserId;

        var response = await admin.PostAsJsonAsync($"/api/users/{id}/block", new BlockRequest { Reason = "проверка" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task FailedLogin_And_Forbidden_And_NotFound_GoToAudit()
    {
        await LoginAsync("kuznetsov@electrofix.ru", "не тот пароль");
        var master = await factory.ClientAsAsync("sokolov@electrofix.ru");
        Assert.Equal(HttpStatusCode.Forbidden, (await master.GetAsync("/api/users")).StatusCode);
        var notFound = await master.GetAsync("/api/admin-secret");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Equal("Такого адреса в Api нет", (await notFound.Content.ReadFromJsonAsync<ErrorResponse>())!.Message);

        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        Assert.Contains(await AuditAsync(admin, "action=LOGIN_FAILED"), a => a.AfterData!.Contains("kuznetsov@electrofix.ru"));
        Assert.Contains(await AuditAsync(admin, "action=ACCESS_DENIED&user=sokolov"), a => a.AfterData!.Contains("/api/users"));
        Assert.Contains(await AuditAsync(admin, "action=NOT_FOUND"), a => a.AfterData!.Contains("/api/admin-secret"));
    }

    [Fact]
    public async Task AdminPanelLogin_ByNotAdmin_Returns403AndIsLogged()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "vasilyeva@electrofix.ru", Password = ApiFactory.SeedPassword, App = "admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        Assert.Contains(await AuditAsync(admin, "action=ACCESS_DENIED"), a => a.AfterData!.Contains("admin-panel"));
    }

    [Fact]
    public async Task Audit_DateFilter_Works()
    {
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        var tomorrow = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");
        var today = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

        Assert.Empty(await AuditAsync(admin, $"from={tomorrow}"));
        Assert.NotEmpty(await AuditAsync(admin, $"from={today}&action=LOGIN"));
    }

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });

    private static async Task<UserDto> FindUserAsync(HttpClient admin, string email) =>
        (await admin.GetFromJsonAsync<PagedResult<UserDto>>($"/api/users?search={email}"))!.Items.Single();

    private static async Task<IReadOnlyList<AuditEntryDto>> AuditAsync(HttpClient admin, string query) =>
        (await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>($"/api/audit?size=100&{query}"))!.Items;
}
