using ElectroFix.Shared;
using ElectroFix.Web.Infrastructure;
using ElectroFix.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Web.Controllers;

// Личный кабинет клиента. Всё идёт через /api/my — Api сам отдаёт только данные этого клиента
[Authorize(Roles = RoleCodes.Client)]
public class CabinetController(ApiClient api) : SiteController
{
    private static readonly string[] Finished = ["closed", "cancelled"];

    public async Task<IActionResult> Index()
    {
        var profile = await api.GetAsync<ClientDto>("/api/my/profile");
        var checkins = await api.AllAsync<CheckinDto>("/api/my/checkins?sort=slot");
        var orders = await api.AllAsync<WorkOrderDto>("/api/my/work-orders?sort=opened,desc");
        return View(new CabinetHomePage
        {
            Profile = profile,
            // старые записи, на которые не пришли, тут не нужны — они есть во «Все записи»
            Upcoming = checkins.Where(c => c.Status is "booked" or "confirmed" && c.SlotEnd > DateTime.UtcNow).ToList(),
            Active = orders.Where(o => !Finished.Contains(o.Status)).ToList(),
            WaitingDecision = orders.Where(o => o.Status == "estimate").ToList(),
            WithDebt = orders.Where(o => o.InvoiceStatus is "unpaid" or "partially_paid").ToList(),
        });
    }

    // ---------- устройства ----------

    public async Task<IActionResult> Devices() =>
        View(new DevicesPage { Devices = await api.AllAsync<DeviceDto>("/api/my/devices") });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Devices([Bind(Prefix = "NewDevice")] DeviceSaveRequest form, bool book = false)
    {
        DeviceDto? added = null;
        if (ModelState.IsValid && await TryApiAsync(async () => added = await api.PostAsync<DeviceDto>("/api/my/devices", form), "NewDevice"))
        {
            TempData["Success"] = $"Устройство «{added!.Brand} {added.Model}» добавлено";
            // если добавляли со страницы записи — возвращаю туда с выбранным устройством
            return book ? RedirectToAction(nameof(Book), new { deviceId = added.DeviceId }) : RedirectToAction(nameof(Devices));
        }
        if (book)
        {
            return View(nameof(Book), await BookPageAsync(new CheckinSaveRequest(), form));
        }
        return View(new DevicesPage { Devices = await api.AllAsync<DeviceDto>("/api/my/devices"), NewDevice = form });
    }

    // ---------- записи на приём ----------

    public async Task<IActionResult> Checkins([FromQuery] StatusListQuery q)
    {
        q.Sort ??= "slot,desc";
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<CheckinDto>>("/api/my/checkins" + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> Book(int? deviceId)
    {
        // по умолчанию — завтра в 10:00 по Москве
        var form = new CheckinSaveRequest
        {
            DeviceId = deviceId ?? 0,
            SlotStart = HtmlHelpers.MoscowNow.Date.AddDays(1).AddHours(10),
        };
        return View(await BookPageAsync(form, new DeviceSaveRequest()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book([Bind(Prefix = "Form")] CheckinSaveRequest form)
    {
        if (ModelState.IsValid && await TryApiAsync(() => api.PostAsync<CheckinDto>("/api/my/checkins", form), "Form"))
        {
            TempData["Success"] = "Вы записаны. Приёмщик подтвердит запись, статус будет виден здесь";
            return RedirectToAction(nameof(Checkins));
        }
        return View(await BookPageAsync(form, new DeviceSaveRequest()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        await RunAndNotifyAsync(() => api.PostAsync($"/api/my/checkins/{id}/cancel"), "Запись отменена");
        return RedirectToAction(nameof(Checkins));
    }

    // ---------- ремонты ----------

    public async Task<IActionResult> Orders([FromQuery] WorkOrderListQuery q)
    {
        q.Sort ??= "opened,desc";
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<WorkOrderDto>>("/api/my/work-orders" + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> Order(int id) =>
        View(new OrderPage { Order = await api.GetAsync<WorkOrderDetailsDto>($"/api/my/work-orders/{id}"), Mode = RoleCodes.Client });

    // клиент отметил, какие позиции сметы согласен оплатить
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(int id, int[] approvedServiceIds, int[] approvedPartIds)
    {
        var request = new EstimateDecisionRequest { ApprovedServiceIds = approvedServiceIds, ApprovedPartIds = approvedPartIds };
        await RunAndNotifyAsync(() => api.PostAsync($"/api/my/work-orders/{id}/estimate", request),
            approvedServiceIds.Length + approvedPartIds.Length == 0
                ? "Вы отказались от сметы. Приёмщик свяжется с вами"
                : "Спасибо! Смета согласована, передаём заказ мастеру");
        return RedirectToAction(nameof(Order), new { id });
    }

    private async Task<BookPage> BookPageAsync(CheckinSaveRequest form, DeviceSaveRequest newDevice) => new()
    {
        Devices = await api.AllAsync<DeviceDto>("/api/my/devices"),
        Form = form,
        NewDevice = newDevice,
    };
}
