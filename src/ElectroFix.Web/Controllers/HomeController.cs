using ElectroFix.Shared;
using ElectroFix.Web.Infrastructure;
using ElectroFix.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ElectroFix.Web.Controllers;

// Главная для всех без входа: прайс, проверка статуса ремонта и заявка на ремонт
[AllowAnonymous]
public class HomeController(ApiClient api) : SiteController
{
    // service — работа, которую выбрали в прайсе кнопкой «Выбрать» (если JS выключен)
    public async Task<IActionResult> Index(int? service) =>
        View(await PageAsync(new HomePage { Request = new RepairRequestSaveRequest { ServiceId = service } }));

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SiteLimits.PublicForms)]
    public async Task<IActionResult> Status([Bind(Prefix = nameof(HomePage.StatusQuery))] RepairStatusQuery q)
    {
        RepairStatusDto? status = null;
        if (ModelState.IsValid)
        {
            await TryApiAsync(async () =>
                status = await api.GetAsync<RepairStatusDto>("/api/public/status" + ListUrl.ToApiQuery(q)),
                nameof(HomePage.StatusQuery));
        }
        var page = await PageAsync(new HomePage { StatusQuery = q, Posted = "status" });
        page.Status = status;
        return View(nameof(Index), page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SiteLimits.PublicForms)]
    public async Task<IActionResult> RequestRepair([Bind(Prefix = nameof(HomePage.Request))] RepairRequestSaveRequest r)
    {
        // телефон пишут как попало («8 900 123-45-67»), а Api ждёт +7XXXXXXXXXX — привожу сам и проверяю заново
        r.Phone = NormalizePhone(r.Phone);
        ModelState.Clear();
        if (TryValidateModel(r, nameof(HomePage.Request))
            && await TryApiAsync(async () =>
            {
                var created = await api.PostAsync<Dictionary<string, int>>("/api/public/requests", r);
                TempData["RequestSent"] = $"Заявка № {created["requestId"]} отправлена. Приёмщик перезвонит на {r.Phone}, "
                                          + "уточнит детали и запишет на удобное время.";
            }, nameof(HomePage.Request)))
        {
            return Redirect(Url.Action(nameof(Index)) + "#request");
        }
        return View(nameof(Index), await PageAsync(new HomePage { Request = r, Posted = "request" }));
    }

    // сюда ведёт UseExceptionHandler, если что-то упало в самом сайте
    public IActionResult Error()
    {
        ViewData["Message"] = "Что-то пошло не так. Попробуйте ещё раз чуть позже.";
        return View("ApiError");
    }

    // прайс небольшой (около 30 работ), поэтому беру его целиком и делю на группы прямо на странице
    private async Task<HomePage> PageAsync(HomePage page)
    {
        page.Services.AddRange(await api.AllAsync<ServiceDto>("/api/catalog/services?sort=name"));
        return page;
    }

    public static string NormalizePhone(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsAsciiDigit).ToArray());
        return digits.Length switch
        {
            11 when digits[0] is '7' or '8' => "+7" + digits[1..],
            10 => "+7" + digits,
            _ => phone?.Trim() ?? "",
        };
    }
}
