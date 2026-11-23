using ElectroFix.Shared;
using ElectroFix.Web.Infrastructure;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Web.Controllers;

// Рабочее место мастера: список своих заказ-нарядов. Сама карточка — в OrdersController
[Authorize(Roles = RoleCodes.Master)]
public class MasterController(ApiClient api) : SiteController
{
    public async Task<IActionResult> Index([FromQuery] WorkOrderListQuery q)
    {
        // мастер видит только свои: id беру из cookie, а не из адреса
        q.MasterId = UserId;
        q.Sort ??= "opened,desc";
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<WorkOrderDto>>("/api/work-orders" + ListUrl.ToApiQuery(q)));
    }
}
