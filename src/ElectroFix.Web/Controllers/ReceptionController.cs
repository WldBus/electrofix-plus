using ElectroFix.Shared;
using ElectroFix.Web.Infrastructure;
using ElectroFix.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Web.Controllers;

// Рабочее место приёмщика: записи на приём, клиенты, приёмка устройства.
// Сами заказ-наряды (смета, мастер, закрытие, оплата) — в OrdersController
[Authorize(Roles = RoleCodes.Receptionist)]
public class ReceptionController(ApiClient api) : SiteController
{
    // ---------- записи ----------

    public async Task<IActionResult> Index([FromQuery] StatusListQuery q)
    {
        q.Sort ??= "slot,desc";
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<CheckinDto>>("/api/checkins" + ListUrl.ToApiQuery(q)));
    }

    // подтвердить / отменить / неявка
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckinStatus(int id, string status, string? returnUrl)
    {
        await RunAndNotifyAsync(() => api.PostAsync($"/api/checkins/{id}/status", new StatusRequest { Status = status }),
            $"Статус записи: «{Labels.Of(Labels.CheckinStatuses, status)}»");
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    // приёмка устройства: осмотр и открытие заказ-наряда
    public async Task<IActionResult> Open(int id) =>
        View(new OpenPage { Checkin = await api.GetAsync<CheckinDto>($"/api/checkins/{id}") });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Open(int id, [Bind(Prefix = "Form")] OpenWorkOrderRequest form)
    {
        OpenedDto? opened = null;
        if (ModelState.IsValid && await TryApiAsync(async () => opened = await api.PostAsync<OpenedDto>($"/api/checkins/{id}/open", form), "Form"))
        {
            TempData["Success"] = "Устройство принято, заказ-наряд открыт. Добавьте диагностику в смету";
            return RedirectToAction("Details", "Orders", new { id = opened!.WorkOrderId });
        }
        return View(new OpenPage { Checkin = await api.GetAsync<CheckinDto>($"/api/checkins/{id}"), Form = form });
    }

    // ---------- клиенты ----------

    public async Task<IActionResult> Clients([FromQuery] ClientListQuery q)
    {
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<ClientDto>>("/api/clients" + ListUrl.ToApiQuery(q)));
    }

    public IActionResult NewClient() => View(new ClientSaveRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewClient(ClientSaveRequest form)
    {
        ClientDto? client = null;
        if (ModelState.IsValid && await TryApiAsync(async () => client = await api.PostAsync<ClientDto>("/api/clients", form)))
        {
            TempData["Success"] = "Клиент добавлен. Теперь добавьте устройство и запишите на приём";
            return RedirectToAction(nameof(Book), new { clientId = client!.ClientId });
        }
        return View(form);
    }

    // ---------- запись клиента на приём ----------

    public async Task<IActionResult> Book(int clientId, int? deviceId)
    {
        var form = new CheckinSaveRequest
        {
            DeviceId = deviceId ?? 0,
            SlotStart = HtmlHelpers.MoscowNow.AddMinutes(30 - HtmlHelpers.MoscowNow.Minute % 15),
        };
        return View(await BookPageAsync(clientId, form, new DeviceSaveRequest()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(int clientId, [Bind(Prefix = "Form")] CheckinSaveRequest form)
    {
        if (ModelState.IsValid && await TryApiAsync(() => api.PostAsync<CheckinDto>("/api/checkins", form), "Form"))
        {
            TempData["Success"] = "Клиент записан, запись сразу подтверждена";
            return RedirectToAction(nameof(Index));
        }
        return View(await BookPageAsync(clientId, form, new DeviceSaveRequest()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDevice(int clientId, [Bind(Prefix = "NewDevice")] DeviceSaveRequest form)
    {
        form.ClientId = clientId;
        DeviceDto? added = null;
        if (ModelState.IsValid && await TryApiAsync(async () => added = await api.PostAsync<DeviceDto>("/api/devices", form), "NewDevice"))
        {
            TempData["Success"] = $"Устройство «{added!.Brand} {added.Model}» добавлено";
            return RedirectToAction(nameof(Book), new { clientId, deviceId = added.DeviceId });
        }
        return View(nameof(Book), await BookPageAsync(clientId, new CheckinSaveRequest(), form));
    }

    // ---------- заказ-наряды ----------

    public async Task<IActionResult> Orders([FromQuery] WorkOrderListQuery q)
    {
        q.Sort ??= "opened,desc";
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<WorkOrderDto>>("/api/work-orders" + ListUrl.ToApiQuery(q)));
    }

    private async Task<BookPage> BookPageAsync(int clientId, CheckinSaveRequest form, DeviceSaveRequest newDevice) => new()
    {
        Client = await api.GetAsync<ClientDto>($"/api/clients/{clientId}"),
        Devices = await api.AllAsync<DeviceDto>($"/api/devices?clientId={clientId}"),
        Form = form,
        NewDevice = newDevice,
    };

    private record OpenedDto(int WorkOrderId);
}
