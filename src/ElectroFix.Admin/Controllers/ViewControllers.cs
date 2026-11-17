using ElectroFix.Admin.Infrastructure;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Admin.Controllers;

// Разделы «только просмотр»: у админа нет прав менять заказ-наряды, деньги и склад.
// Это решают сотрудники в своих рабочих местах на сайте

public class HomeController(ApiClient api, StatsClient stats) : AdminController
{
    // главная: цифры и графики от сервиса статистики + последние заказ-наряды из Api
    public async Task<IActionResult> Index(int months = 12)
    {
        ViewBag.Months = months;
        ViewBag.Recent = await api.GetAsync<PagedResult<WorkOrderDto>>("/api/work-orders?size=5&sort=opened,desc");
        return View(await stats.DashboardAsync(months));
    }

    [AllowAnonymous]
    public IActionResult Error() => View("ApiError");
}

// безопасность: входы, неудачные попытки, доступ без прав и подозрительные IP
public class SecurityController(ApiClient api, StatsClient stats) : AdminController
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Events = await api.GetAsync<PagedResult<AuditEntryDto>>("/api/audit?security=true&size=15");
        return View(await stats.DashboardAsync(1));
    }
}

public class DevicesController(ApiClient api) : AdminController
{
    public async Task<IActionResult> Index([FromQuery] ListQuery q, int? clientId)
    {
        ViewBag.Query = q;
        var query = ListUrl.ToApiQuery(new { q.Search, q.Sort, q.Page, q.Size, clientId });
        return View(await api.GetAsync<PagedResult<DeviceDto>>("/api/devices" + query));
    }
}

public class CheckinsController(ApiClient api) : AdminController
{
    public async Task<IActionResult> Index([FromQuery] StatusListQuery q)
    {
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<CheckinDto>>("/api/checkins" + ListUrl.ToApiQuery(q)));
    }
}

public class WorkOrdersController(ApiClient api) : AdminController
{
    public async Task<IActionResult> Index([FromQuery] WorkOrderListQuery q)
    {
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<WorkOrderDto>>("/api/work-orders" + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> Details(int id) =>
        View(await api.GetAsync<WorkOrderDetailsDto>($"/api/work-orders/{id}"));
}

public class ReportsController(ApiClient api, StatsClient stats) : AdminController
{
    public async Task<IActionResult> Stock([FromQuery] ListQuery q)
    {
        ViewBag.Query = q;
        ViewBag.BelowMin = (await stats.DashboardAsync(1)).PartsBelowMin;
        return View(await api.GetAsync<PagedResult<StockBalanceDto>>("/api/stock" + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> StockMoves([FromQuery] StockMoveListQuery q)
    {
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<StockMoveDto>>("/api/stock-moves" + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> Invoices([FromQuery] StatusListQuery q)
    {
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<InvoiceDto>>("/api/invoices" + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> Payments([FromQuery] ListQuery q)
    {
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<PaymentDto>>("/api/payments" + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> Warranty([FromQuery] StatusListQuery q)
    {
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<WarrantyCaseDto>>("/api/warranty-cases" + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> Audit([FromQuery] AuditListQuery q)
    {
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<AuditEntryDto>>("/api/audit" + ListUrl.ToApiQuery(q)));
    }
}
