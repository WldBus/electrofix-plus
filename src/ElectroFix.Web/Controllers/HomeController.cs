using ElectroFix.Shared;
using ElectroFix.Web.Infrastructure;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Web.Controllers;

// Главная: открытый прайс работ, без входа
[AllowAnonymous]
public class HomeController(ApiClient api) : SiteController
{
    public async Task<IActionResult> Index([FromQuery] CatalogListQuery q)
    {
        q.Deleted = false;
        q.Size = 15;
        ViewBag.Query = q;
        // категории для фильтра беру из полного прайса (он небольшой)
        ViewBag.Categories = (await api.AllAsync<ServiceDto>("/api/catalog/services"))
            .Select(s => s.Category).Distinct().Order().ToList();
        return View(await api.GetAsync<PagedResult<ServiceDto>>("/api/catalog/services" + ListUrl.ToApiQuery(q)));
    }

    // сюда ведёт UseExceptionHandler, если что-то упало в самом сайте
    public IActionResult Error()
    {
        ViewData["Message"] = "Что-то пошло не так. Попробуйте ещё раз чуть позже.";
        return View("ApiError");
    }
}
