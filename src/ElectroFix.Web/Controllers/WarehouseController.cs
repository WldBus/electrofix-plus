using ElectroFix.Shared;
using ElectroFix.Web.Infrastructure;
using ElectroFix.Web.Models;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Web.Controllers;

// Рабочее место закупщика: что выдать мастерам, приход партий, остатки и движения
[Authorize(Roles = RoleCodes.Purchaser)]
public class WarehouseController(ApiClient api) : SiteController
{
    // согласованные запчасти, которые ещё не выдали в ремонт
    public async Task<IActionResult> Index()
    {
        var items = await api.GetAsync<List<PartToIssueDto>>("/api/stock/to-issue");
        var batches = new Dictionary<int, List<StockBalanceDto>>();
        foreach (var partId in items.Select(i => i.PartId).Distinct())
        {
            batches[partId] = await api.GetAsync<List<StockBalanceDto>>($"/api/stock/batches?partId={partId}");
        }
        return View(new ToIssuePage { Items = items, Batches = batches });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(StockIssueRequest form)
    {
        await RunAndNotifyAsync(() => api.PostAsync("/api/stock/issue", form),
            $"Выдано {HtmlHelpers.Num(form.Quantity)} из партии {form.Batch}");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Receipt(int? partId) =>
        View(new ReceiptPage { Parts = await PartsAsync(), Form = new StockReceiptRequest { PartId = partId ?? 0 } });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receipt([Bind(Prefix = "Form")] StockReceiptRequest form)
    {
        if (ModelState.IsValid && await TryApiAsync(() => api.PostAsync("/api/stock/receipt", form), "Form"))
        {
            TempData["Success"] = $"Партия {form.Batch} оприходована в ячейку {form.StorageCell}";
            return RedirectToAction(nameof(Stock), new { search = form.Batch });
        }
        return View(new ReceiptPage { Parts = await PartsAsync(), Form = form });
    }

    // остатки по партиям + сверху детали, которых меньше минимального запаса
    public async Task<IActionResult> Stock([FromQuery] ListQuery q)
    {
        ViewBag.Query = q;
        var parts = await PartsAsync();
        var stock = await api.AllAsync<StockBalanceDto>("/api/stock");
        var available = stock.GroupBy(s => s.PartId).ToDictionary(g => g.Key, g => g.Sum(s => s.Available));
        ViewBag.BelowMin = parts
            .Select(p => new BelowMinItem(p, available.GetValueOrDefault(p.PartId)))
            .Where(x => x.Available < x.Part.MinStock)
            .OrderBy(x => x.Available - x.Part.MinStock)
            .ToList();
        return View(await api.GetAsync<PagedResult<StockBalanceDto>>("/api/stock" + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> Moves([FromQuery] StockMoveListQuery q)
    {
        q.Sort ??= "moved,desc";
        ViewBag.Query = q;
        return View(await api.GetAsync<PagedResult<StockMoveDto>>("/api/stock-moves" + ListUrl.ToApiQuery(q)));
    }

    private Task<List<PartDto>> PartsAsync() => api.AllAsync<PartDto>("/api/parts?sort=name");
}
