using System.Net;
using System.Net.Http.Json;

using ElectroFix.Shared;

namespace ElectroFix.Api.Tests;

// Заявка с сайта без входа и проверка статуса ремонта по номеру квитанции
public class RequestsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static RepairRequestSaveRequest Request(string method = "self") => new()
    {
        DeviceType = "Смартфон",
        DeviceModel = "Xiaomi Redmi Note 12",
        Problem = "Не заряжается, разъём шатается",
        DeliveryMethod = method,
        Branch = method == "self" ? Branches.All[0] : null,
        Address = method == "self" ? null : "Москва, ул. Тестовая, д. 1",
        ServiceId = 12,
        ClientName = "Тестов Тест",
        Phone = "+79001234500",
    };

    [Fact]
    public async Task Guest_CreatesRequest_ReceptionistClosesIt()
    {
        var guest = factory.CreateClient();
        var created = await guest.PostAsJsonAsync("/api/public/requests", Request());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<Dictionary<string, int>>())!["requestId"];

        var receptionist = await factory.ClientAsAsync("petrova@electrofix.ru");
        var list = await receptionist.GetFromJsonAsync<PagedResult<RepairRequestDto>>("/api/requests?status=new");
        var request = Assert.Single(list!.Items, r => r.RequestId == id);
        Assert.Equal("Замена разъёма зарядки", request.ServiceName);

        // отклонить без причины нельзя
        var noReason = await receptionist.PostAsJsonAsync($"/api/requests/{id}/status", new RequestStatusChange { Status = "rejected" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReason.StatusCode);

        var done = await receptionist.PostAsJsonAsync($"/api/requests/{id}/status",
            new RequestStatusChange { Status = "done", Comment = "Клиент заведён, записан" });
        var dto = await done.Content.ReadFromJsonAsync<RepairRequestDto>();
        Assert.Equal("done", dto!.Status);
        Assert.NotNull(dto.ProcessedAt);
        Assert.Equal("Петрова Мария Сергеевна", dto.ProcessedBy);

        // закрытую заявку второй раз не закрыть
        var again = await receptionist.PostAsJsonAsync($"/api/requests/{id}/status", new RequestStatusChange { Status = "processing" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Request_NeedsBranchOrAddress()
    {
        var guest = factory.CreateClient();
        var courier = Request("courier");
        courier.Address = null;
        var response = await guest.PostAsJsonAsync("/api/public/requests", courier);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.True(error!.Errors!.ContainsKey("address"));

        var badPhone = Request();
        badPhone.Phone = "8900";
        response = await guest.PostAsJsonAsync("/api/public/requests", badPhone);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Requests_ClientAndGuestCannotRead_AdminCannotProcess()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/requests")).StatusCode);
        var client = await factory.ClientAsAsync("novikov.sp@mail.ru");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/requests")).StatusCode);

        // админ БД заявки видит, но обрабатывает их приёмщик
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/requests")).StatusCode);
        var process = await admin.PostAsJsonAsync("/api/requests/1/status", new RequestStatusChange { Status = "processing" });
        Assert.Equal(HttpStatusCode.Forbidden, process.StatusCode);
    }

    [Fact]
    public async Task Status_ByNumberAndPhone()
    {
        var guest = factory.CreateClient();
        // ЗН-2026-00002 из seed.sql: смартфон клиента с телефоном на ...5678, в работе
        var status = await guest.GetFromJsonAsync<RepairStatusDto>("/api/public/status?number=зн 2026 2&phone=5678");
        Assert.Equal("ЗН-2026-00002", status!.Number);
        Assert.Equal("in_progress", status.Status);
        Assert.Contains("Galaxy A54", status.Device);

        // верный номер, чужой телефон — ответ такой же, как на несуществующий номер
        var wrongPhone = await guest.GetAsync("/api/public/status?number=ЗН-2026-00002&phone=0000");
        var noNumber = await guest.GetAsync("/api/public/status?number=ЗН-2026-09999&phone=5678");
        Assert.Equal(HttpStatusCode.NotFound, wrongPhone.StatusCode);
        Assert.Equal(await noNumber.Content.ReadAsStringAsync(), await wrongPhone.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Status_LocksNumberAfterFiveMistakes()
    {
        var guest = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var miss = await guest.GetAsync($"/api/public/status?number=ЗН-2026-00004&phone=000{i}");
            Assert.Equal(HttpStatusCode.NotFound, miss.StatusCode);
        }
        // теперь даже верный телефон не поможет, пока не пройдёт 15 минут
        var locked = await guest.GetAsync("/api/public/status?number=ЗН-2026-00004&phone=4321");
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
    }
}
