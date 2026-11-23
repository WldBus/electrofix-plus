using System.Net;
using System.Net.Http.Json;

using ElectroFix.Shared;

namespace ElectroFix.Api.Tests;

// Весь путь ремонта через Api, как его пройдут люди на сайте:
// клиент записался → приёмщик принял → смета → клиент согласовал →
// закупщик выдал запчасть → мастер сделал → закрытие, счёт и оплата
public class WorkflowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task FullRepair_FromBookingToPayment()
    {
        var client = await factory.ClientAsAsync("novikov.sp@mail.ru");
        var receptionist = await factory.ClientAsAsync("petrova@electrofix.ru");
        var purchaser = await factory.ClientAsAsync("kuznetsov@electrofix.ru");
        var master = await factory.ClientAsAsync("sokolov@electrofix.ru");

        // клиент: своя карточка привязана по email, добавляет устройство и записывается
        var profile = await client.GetFromJsonAsync<ClientDto>("/api/my/profile");
        Assert.Equal("novikov.sp@mail.ru", profile!.Email);
        var device = await Ok<DeviceDto>(await client.PostAsJsonAsync("/api/my/devices", new DeviceSaveRequest
        {
            Type = "Посудомоечная машина",
            Brand = "Bosch",
            Model = "SMS25",
            SerialNumber = "TEST-DW-0001",
        }));
        var checkin = await Ok<CheckinDto>(await client.PostAsJsonAsync("/api/my/checkins", new CheckinSaveRequest
        {
            DeviceId = device.DeviceId,
            SlotStart = DateTime.Now.AddDays(1),
            Defects = "Не сливает воду",
        }));
        Assert.Equal("booked", checkin.Status);

        // приёмщик: подтверждает, принимает устройство, составляет смету и отправляет клиенту
        await Ok(await receptionist.PostAsJsonAsync($"/api/checkins/{checkin.CheckinId}/status", new StatusRequest { Status = "confirmed" }));
        var opened = await Ok<OpenedDto>(await receptionist.PostAsJsonAsync($"/api/checkins/{checkin.CheckinId}/open",
            new OpenWorkOrderRequest { ConditionState = "Без видимых повреждений" }));
        var id = opened.WorkOrderId;
        await Ok(await receptionist.PostAsJsonAsync($"/api/work-orders/{id}/services", new OrderServiceAddRequest { ServiceId = 1 }));
        await Ok(await receptionist.PostAsJsonAsync($"/api/work-orders/{id}/services", new OrderServiceAddRequest { ServiceId = 2 }));
        var order = await Ok<WorkOrderDetailsDto>(await receptionist.PostAsJsonAsync($"/api/work-orders/{id}/parts",
            new OrderPartAddRequest { PartId = 1, Quantity = 1, Price = 900 }));
        order = await Ok<WorkOrderDetailsDto>(await receptionist.PostAsync($"/api/work-orders/{id}/send-estimate", null));
        Assert.Equal("estimate", order.Header.Status);

        // клиент: соглашается на диагностику и запчасть, от второй работы отказывается
        order = await Ok<WorkOrderDetailsDto>(await client.PostAsJsonAsync($"/api/my/work-orders/{id}/estimate", new EstimateDecisionRequest
        {
            ApprovedServiceIds = [order.Services.Single(s => s.ServiceId == 1).OrderServiceId],
            ApprovedPartIds = [order.Parts.Single().OrderPartId],
        }));
        Assert.Equal("approved", order.Header.Status);
        Assert.Equal("rejected", order.Services.Single(s => s.ServiceId == 2).ApprovalStatus);

        // приёмщик назначает мастера
        var sokolov = (await receptionist.GetFromJsonAsync<List<MasterDto>>("/api/masters"))!
            .Single(m => m.FullName.StartsWith("Соколов"));
        order = await Ok<WorkOrderDetailsDto>(await receptionist.PostAsJsonAsync($"/api/work-orders/{id}/assign",
            new AssignMasterRequest { MasterId = sokolov.UserId }));
        Assert.Equal("assigned", order.Header.Status);

        // закупщик: деталь в списке «к выдаче», приходует партию и выдаёт
        var toIssue = (await purchaser.GetFromJsonAsync<List<PartToIssueDto>>("/api/stock/to-issue"))!;
        Assert.Contains(toIssue, p => p.WorkOrderId == id && p.PartId == 1 && p.Remaining == 1);
        await Ok(await purchaser.PostAsJsonAsync("/api/stock/receipt", new StockReceiptRequest
        {
            PartId = 1,
            Quantity = 5,
            Batch = "TEST-B1",
            StorageCell = "A-01-03",
        }));
        await Ok(await purchaser.PostAsJsonAsync("/api/stock/issue", new StockIssueRequest
        {
            WorkOrderId = id,
            PartId = 1,
            Batch = "TEST-B1",
            Quantity = 1,
        }));
        Assert.DoesNotContain((await purchaser.GetFromJsonAsync<List<PartToIssueDto>>("/api/stock/to-issue"))!, p => p.WorkOrderId == id);

        // мастер: в работу, отмечает время, на проверку
        await Ok(await master.PostAsJsonAsync($"/api/work-orders/{id}/status", new StatusRequest { Status = "in_progress" }));
        var line = order.Services.Single(s => s.ApprovalStatus == "approved").OrderServiceId;
        await Ok(await master.PutAsJsonAsync($"/api/work-orders/{id}/services/{line}/time", new ActualTimeRequest { ActualTime = 0.5m }));
        await Ok(await master.PostAsJsonAsync($"/api/work-orders/{id}/status", new StatusRequest { Status = "pdi" }));

        // приёмщик: проверка пройдена, закрывает — появляется счёт; клиент платит в кассе
        await Ok(await receptionist.PostAsJsonAsync($"/api/work-orders/{id}/status", new StatusRequest { Status = "ready" }));
        order = await Ok<WorkOrderDetailsDto>(await receptionist.PostAsync($"/api/work-orders/{id}/close", null));
        Assert.Equal("closed", order.Header.Status);
        var invoice = order.Invoices.Single();
        await Ok(await receptionist.PostAsJsonAsync("/api/payments", new PaymentSaveRequest
        {
            InvoiceId = invoice.InvoiceId,
            Amount = invoice.Amount,
            Method = "cash",
        }));

        // клиент видит закрытый и оплаченный заказ-наряд у себя в кабинете
        var mine = await client.GetFromJsonAsync<WorkOrderDetailsDto>($"/api/my/work-orders/{id}");
        Assert.Equal("paid", mine!.Invoices.Single().Status);
    }

    [Fact]
    public async Task Client_CannotSeeSomeoneElsesWorkOrder()
    {
        // заказ-наряд №1 из тестовых данных принадлежит Новикову
        var morozova = await factory.ClientAsAsync("morozova.av@yandex.ru");

        Assert.Equal(HttpStatusCode.NotFound, (await morozova.GetAsync("/api/my/work-orders/1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await morozova.GetAsync("/api/work-orders/1")).StatusCode);
    }

    [Fact]
    public async Task Client_CannotBookOnSomeoneElsesDevice()
    {
        var morozova = await factory.ClientAsAsync("morozova.av@yandex.ru");

        var response = await morozova.PostAsJsonAsync("/api/my/checkins", new CheckinSaveRequest
        {
            DeviceId = 1,
            SlotStart = DateTime.Now.AddDays(2),
            Defects = "чужое",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Master_CannotTouchOtherMastersOrder_AndAdminCannotOperate()
    {
        var admin = await factory.ClientAsAsync("admin@electrofix.ru");
        var popov = await factory.ClientAsAsync("popov@electrofix.ru");
        var orders = await admin.GetFromJsonAsync<PagedResult<WorkOrderDto>>("/api/work-orders?size=100");
        var foreign = orders!.Items.First(w => w.MasterId != null && w.Master != null && !w.Master.StartsWith("П"));

        var response = await popov.PostAsJsonAsync($"/api/work-orders/{foreign.WorkOrderId}/status", new StatusRequest { Status = "pdi" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // админ БД операции только смотрит
        var adminTry = await admin.PostAsync($"/api/work-orders/{foreign.WorkOrderId}/close", null);
        Assert.Equal(HttpStatusCode.Forbidden, adminTry.StatusCode);
    }

    [Fact]
    public async Task Booking_InThePast_Returns422OnField()
    {
        var client = await factory.ClientAsAsync("novikov.sp@mail.ru");

        var response = await client.PostAsJsonAsync("/api/my/checkins", new CheckinSaveRequest
        {
            DeviceId = 1,
            SlotStart = DateTime.Now.AddDays(-1),
            Defects = "вчера",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.True(error!.Errors!.ContainsKey("slotStart"));
    }

    [Fact]
    public async Task PublicCatalog_OpenWithoutLogin()
    {
        var services = await factory.CreateClient().GetFromJsonAsync<PagedResult<ServiceDto>>("/api/catalog/services");

        Assert.NotEmpty(services!.Items);
    }

    private record OpenedDto(int WorkOrderId);

    private static async Task Ok(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            Assert.Fail($"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    private static async Task<T> Ok<T>(HttpResponseMessage response)
    {
        await Ok(response);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
}
