using ElectroFix.Shared;
using ElectroFix.Web.Infrastructure;
using ElectroFix.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Web.Controllers;

// Карточка заказ-наряда для сотрудников. Приёмщик ведёт смету, назначает мастера, закрывает
// и принимает оплату; мастер работает только со своими заказ-нарядами. Что кому можно,
// всё равно проверяет Api — тут я просто не показываю лишние кнопки
[Authorize(Roles = RoleCodes.Receptionist + "," + RoleCodes.Master)]
public class OrdersController(ApiClient api) : SiteController
{
    public async Task<IActionResult> Details(int id)
    {
        var order = await api.GetAsync<WorkOrderDetailsDto>($"/api/work-orders/{id}");
        var canAct = Role != RoleCodes.Master || order.Header.MasterId == UserId;
        var editable = canAct && WorkOrderRules.EditableEstimate.Contains(order.Header.Status);
        return View(new OrderPage
        {
            Order = order,
            Mode = Role,
            CanAct = canAct,
            // справочники для формы добавления в смету нужны, только пока смету можно менять
            Services = editable ? await api.AllAsync<ServiceDto>("/api/services?sort=name") : [],
            Parts = editable ? await api.AllAsync<PartDto>("/api/parts?sort=name") : [],
            Masters = Role == RoleCodes.Receptionist && order.Header.Status == "approved"
                ? await api.GetAsync<List<MasterDto>>("/api/masters")
                : [],
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Status(int id, string status)
    {
        await RunAndNotifyAsync(() => api.PostAsync($"/api/work-orders/{id}/status", new StatusRequest { Status = status }),
            $"Статус: {Labels.Of(Labels.WorkOrderStatuses, status)}");
        return Back(id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddService(int id, OrderServiceAddRequest form)
    {
        await RunAndNotifyAsync(() => api.PostAsync($"/api/work-orders/{id}/services", form),
            "Работа добавлена в черновик сметы");
        return Back(id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPart(int id, OrderPartAddRequest form)
    {
        await RunAndNotifyAsync(() => api.PostAsync($"/api/work-orders/{id}/parts", form),
            "Запчасть добавлена в черновик сметы");
        return Back(id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveService(int id, int lineId)
    {
        await RunAndNotifyAsync(() => api.DeleteAsync($"/api/work-orders/{id}/services/{lineId}"), "Работа убрана из сметы");
        return Back(id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePart(int id, int lineId)
    {
        await RunAndNotifyAsync(() => api.DeleteAsync($"/api/work-orders/{id}/parts/{lineId}"), "Запчасть убрана из сметы");
        return Back(id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEstimate(int id)
    {
        await RunAndNotifyAsync(() => api.PostAsync($"/api/work-orders/{id}/send-estimate"),
            "Смета отправлена клиенту — он увидит её в кабинете");
        return Back(id);
    }

    // приёмщик отмечает решение клиента, если тот ответил по телефону
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(int id, int[] approvedServiceIds, int[] approvedPartIds)
    {
        var request = new EstimateDecisionRequest { ApprovedServiceIds = approvedServiceIds, ApprovedPartIds = approvedPartIds };
        await RunAndNotifyAsync(() => api.PostAsync($"/api/work-orders/{id}/estimate", request), "Решение клиента по смете сохранено");
        return Back(id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, AssignMasterRequest form)
    {
        await RunAndNotifyAsync(() => api.PostAsync($"/api/work-orders/{id}/assign", form), "Мастер назначен");
        return Back(id);
    }

    // мастер отмечает, сколько реально потратил на работу
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Time(int id, int lineId, ActualTimeRequest form)
    {
        await RunAndNotifyAsync(() => api.PutAsync<WorkOrderDetailsDto>($"/api/work-orders/{id}/services/{lineId}/time", form),
            "Фактическое время сохранено");
        return Back(id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        await RunAndNotifyAsync(() => api.PostAsync($"/api/work-orders/{id}/close"),
            "Заказ-наряд закрыт, счёт выставлен");
        return Back(id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id, PaymentSaveRequest form)
    {
        await RunAndNotifyAsync(() => api.PostAsync("/api/payments", form),
            $"Оплата {HtmlHelpers.Money(form.Amount)} принята");
        return Back(id);
    }

    private RedirectToActionResult Back(int id) => RedirectToAction(nameof(Details), new { id });
}
