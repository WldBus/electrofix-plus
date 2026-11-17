using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using ElectroFix.Shared;

namespace ElectroFix.Api.Tests;

// вход, токены и права по ролям
public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_ReturnsTokensAndRoleFromDatabase()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "ADMIN@electrofix.ru", Password = ApiFactory.SeedPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(auth!.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.Equal(15 * 60, auth.ExpiresIn);
        Assert.Equal(RoleCodes.Admin, auth.User.RoleCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "admin@electrofix.ru", Password = "не тот пароль" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NoToken_Returns401_ButHealthIsOpen()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/clients")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task BrokenToken_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "abc.def.ghi");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/clients")).StatusCode);
    }

    [Fact]
    public async Task Master_CannotManageUsers_Returns403()
    {
        var master = await factory.ClientAsAsync("sokolov@electrofix.ru");

        Assert.Equal(HttpStatusCode.Forbidden, (await master.GetAsync("/api/users")).StatusCode);
        // а заказ-наряды мастеру смотреть можно
        Assert.Equal(HttpStatusCode.OK, (await master.GetAsync("/api/work-orders")).StatusCode);
    }

    [Fact]
    public async Task Client_CannotSeeStaffData_Returns403()
    {
        var client = await factory.ClientAsAsync("novikov.sp@mail.ru");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/work-orders")).StatusCode);
    }

    [Fact]
    public async Task Receptionist_CannotDeleteClientForever_Returns403()
    {
        var receptionist = await factory.ClientAsAsync("petrova@electrofix.ru");

        var response = await receptionist.DeleteAsync("/api/clients/5?hard=true");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Receptionist_CannotCreateService_Returns403()
    {
        var receptionist = await factory.ClientAsAsync("petrova@electrofix.ru");

        var response = await receptionist.PostAsJsonAsync("/api/services",
            new ServiceSaveRequest { Name = "Чужая работа", Category = "Общие", NormHours = 1, Rate = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesToken_OldOneStopsWorking()
    {
        var api = factory.CreateClient();
        var login = await (await api.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "kuznetsov@electrofix.ru", Password = ApiFactory.SeedPassword }))
            .Content.ReadFromJsonAsync<AuthResponse>();

        var first = await api.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = login!.RefreshToken });
        var second = await api.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Logout_KillsRefreshToken()
    {
        var api = factory.CreateClient();
        var login = await (await api.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = "popov@electrofix.ru", Password = ApiFactory.SeedPassword }))
            .Content.ReadFromJsonAsync<AuthResponse>();

        await api.PostAsJsonAsync("/api/auth/logout", new RefreshRequest { RefreshToken = login!.RefreshToken });
        var refresh = await api.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = login.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Register_CreatesClientRole_DuplicateEmailIs409()
    {
        var api = factory.CreateClient();
        var request = new RegisterRequest
        {
            FullName = "Регистров Иван",
            Email = "register@test.ru",
            Phone = "+79990000021",
            Password = "Password123",
        };

        var first = await api.PostAsJsonAsync("/api/auth/register", request);
        var second = await api.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var auth = await first.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(RoleCodes.Client, auth!.User.RoleCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsCurrentUser()
    {
        var master = await factory.ClientAsAsync("vasilyeva@electrofix.ru");

        var me = await master.GetFromJsonAsync<UserDto>("/api/auth/me");

        Assert.Equal("vasilyeva@electrofix.ru", me!.Email);
    }

    [Fact]
    public async Task Admin_CannotTrashHimself_Returns409()
    {
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");

        var response = await admin.DeleteAsync("/api/users/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Changes_AreAuditedWithAuthor()
    {
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        await admin.PutAsJsonAsync("/api/clients/2", new ClientSaveRequest
        {
            DisplayName = "Морозова Анна Викторовна",
            Phone = "+79262345678",
        });

        // триггер аудита должен записать изменение клиента с автором — админом (id 1)
        var audit = await admin.GetFromJsonAsync<PagedResult<AuditEntryDto>>("/api/audit?table=clients&action=UPDATE");

        Assert.Contains(audit!.Items, a => a.ObjectId == 2 && a.UserId == 1);
    }
}
