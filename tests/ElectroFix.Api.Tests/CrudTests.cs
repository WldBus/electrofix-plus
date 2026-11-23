using System.Net;
using System.Net.Http.Json;

using ElectroFix.Shared;

namespace ElectroFix.Api.Tests;

// Проверяю списки, коды ошибок и корзину на настоящей базе с тестовыми данными
public class CrudTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private HttpClient _api = null!;

    // все проверки здесь делаю под админом
    public async Task InitializeAsync() => _api = await factory.ClientAsAsync("admin@electrofix.ru");

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task List_ReturnsPageAndTotal()
    {
        var page = await _api.GetFromJsonAsync<PagedResult<ClientDto>>("/api/clients?size=2&sort=id,asc");

        Assert.Equal(2, page!.Items.Count);
        Assert.Equal(1, page.Page);
        Assert.True(page.Total >= 5);
        Assert.Equal(1, page.Items[0].ClientId);
    }

    [Fact]
    public async Task List_SearchFindsClientByName()
    {
        var page = await _api.GetFromJsonAsync<PagedResult<ClientDto>>("/api/clients?search=новиков");

        // поиск идёт по настоящему ФИО, но админу оно приходит скрытым
        Assert.Single(page!.Items);
        Assert.Equal("Н*****в С. П.", page.Items[0].DisplayName);
        Assert.Equal("novikov.sp@mail.ru", page.Items[0].Email);
    }

    [Fact]
    public async Task List_UnknownSortField_Returns400()
    {
        var response = await _api.GetAsync("/api/clients?sort=password");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_SecondPage_ContinuesFirst()
    {
        var first = await _api.GetFromJsonAsync<PagedResult<ServiceDto>>("/api/services?size=3&page=1&sort=id,asc");
        var second = await _api.GetFromJsonAsync<PagedResult<ServiceDto>>("/api/services?size=3&page=2&sort=id,asc");

        // вторая страница начинается сразу после первой, без повторов и пропусков
        Assert.Equal(2, second!.Page);
        Assert.Equal(first!.Total, second.Total);
        Assert.True(second.Items[0].ServiceId > first.Items[^1].ServiceId);
        Assert.Empty(first.Items.Select(s => s.ServiceId).Intersect(second.Items.Select(s => s.ServiceId)));
    }

    [Fact]
    public async Task List_SortDesc_AndHugePageSizeIsLimited()
    {
        var page = await _api.GetFromJsonAsync<PagedResult<ServiceDto>>("/api/services?size=100000&sort=id,desc");

        // больше 100 строк за раз не отдаю, чтобы одним запросом не выкачать всю таблицу
        Assert.Equal(100, page!.Size);
        Assert.Equal(page.Items.Select(s => s.ServiceId).OrderDescending(), page.Items.Select(s => s.ServiceId));
    }

    [Fact]
    public async Task List_FilterByClientType()
    {
        var page = await _api.GetFromJsonAsync<PagedResult<ClientDto>>("/api/clients?clientType=legal_entity");

        Assert.NotEmpty(page!.Items);
        Assert.All(page.Items, c => Assert.Equal("legal_entity", c.ClientType));
    }

    [Fact]
    public async Task List_FilterWorkOrdersByStatus()
    {
        var page = await _api.GetFromJsonAsync<PagedResult<WorkOrderDto>>("/api/work-orders?status=closed");

        Assert.All(page!.Items, w => Assert.Equal("closed", w.Status));
        Assert.Contains(page.Items, w => w.WorkOrderNumber == "ЗН-2026-00001");
    }

    [Fact]
    public async Task Create_InvalidFields_Returns422WithFieldErrors()
    {
        var response = await _api.PostAsJsonAsync("/api/clients",
            new ClientSaveRequest { DisplayName = "", Phone = "123" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.True(error!.Errors!.ContainsKey("phone"));
        Assert.True(error.Errors.ContainsKey("displayName"));
    }

    [Fact]
    public async Task Create_LegalEntityWithoutInn_Returns422()
    {
        var response = await _api.PostAsJsonAsync("/api/clients",
            new ClientSaveRequest { ClientType = "legal_entity", DisplayName = "ООО Без ИНН", Phone = "+79990000011" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.True(error!.Errors!.ContainsKey("inn"));
    }

    [Fact]
    public async Task Create_DuplicateInn_Returns409()
    {
        // ИНН 7701234567 уже есть у «ТехноСтроя» из тестовых данных
        var response = await _api.PostAsJsonAsync("/api/clients", new ClientSaveRequest
        {
            ClientType = "legal_entity",
            DisplayName = "ООО Дубль",
            Phone = "+79990000012",
            Inn = "7701234567",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Trash_HidesClient_AndRestoreReturnsIt()
    {
        var created = await (await _api.PostAsJsonAsync("/api/clients", new ClientSaveRequest
        {
            DisplayName = "Корзинов Пётр",
            Phone = "+79990000013",
        })).Content.ReadFromJsonAsync<ClientDto>();
        var id = created!.ClientId;

        Assert.Equal(HttpStatusCode.NoContent, (await _api.DeleteAsync($"/api/clients/{id}")).StatusCode);
        var active = await _api.GetFromJsonAsync<PagedResult<ClientDto>>("/api/clients?search=Корзинов");
        var trash = await _api.GetFromJsonAsync<PagedResult<ClientDto>>("/api/clients?search=Корзинов&deleted=true");
        Assert.Empty(active!.Items);
        Assert.Single(trash!.Items);

        Assert.Equal(HttpStatusCode.NoContent, (await _api.PostAsync($"/api/clients/{id}/restore", null)).StatusCode);
        var restored = await _api.GetFromJsonAsync<ClientDto>($"/api/clients/{id}");
        Assert.Null(restored!.DeletedAt);
    }

    [Fact]
    public async Task HardDelete_ClientWithDevices_Returns409()
    {
        var response = await _api.DeleteAsync("/api/clients/1?hard=true");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task HardDelete_UsedService_Returns409()
    {
        // «Диагностика» есть в сметах заказ-нарядов
        var response = await _api.DeleteAsync("/api/services/1?hard=true");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateService_ChangeRate_Returns422()
    {
        var response = await _api.PutAsJsonAsync("/api/services/1", new ServiceSaveRequest
        {
            Name = "Диагностика",
            Category = "Общие",
            NormHours = 0.5m,
            Rate = 9999,
            WarrantyMonths = 0,
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task TrashLastAdmin_Returns409()
    {
        var response = await _api.DeleteAsync("/api/users/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetMissing_Returns404()
    {
        var response = await _api.GetAsync("/api/work-orders/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkOrderDetails_HasEstimateAndInvoice()
    {
        var details = await _api.GetFromJsonAsync<WorkOrderDetailsDto>("/api/work-orders/1");

        Assert.Equal("ЗН-2026-00001", details!.Header.WorkOrderNumber);
        Assert.NotEmpty(details.Services);
        Assert.NotEmpty(details.Invoices);
    }
}
