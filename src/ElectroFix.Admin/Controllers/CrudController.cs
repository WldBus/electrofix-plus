using ElectroFix.Admin.Infrastructure;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Admin.Controllers;

// Общий CRUD для того, что админ может менять: клиенты, работы, запчасти, пользователи.
// Список (с корзиной), создание, изменение, в корзину, восстановление,
// удаление навсегда и массовое удаление. Вьюхи у каждой сущности свои
public abstract class CrudController<TDto, TForm, TQuery>(ApiClient api) : AdminController
    where TForm : new()
    where TQuery : ListQuery
{
    protected ApiClient Api => api;

    // адрес ресурса в Api, например /api/clients
    protected abstract string ApiPath { get; }

    // запись из Api → данные формы изменения
    protected abstract TForm ToForm(TDto dto);

    // справочники для выпадающих списков формы (роли и т. п.)
    protected virtual Task PrepareFormAsync() => Task.CompletedTask;

    // что ещё показать над списком (у пользователей — статистика)
    protected virtual Task PrepareListAsync() => PrepareFormAsync();

    public async Task<IActionResult> Index([FromQuery] TQuery q)
    {
        ViewBag.Query = q;
        await PrepareListAsync();
        return View(await api.GetAsync<PagedResult<TDto>>(ApiPath + ListUrl.ToApiQuery(q)));
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Id = 0;
        await PrepareFormAsync();
        return View("Edit", new TForm());
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await api.GetAsync<TDto>($"{ApiPath}/{id}");
        ViewBag.Id = id;
        ViewBag.Item = item;
        await PrepareFormAsync();
        return View(ToForm(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, TForm form)
    {
        // сначала проверка на стороне панели (DataAnnotations), потом Api проверит ещё раз
        if (ModelState.IsValid && await TryApiAsync(async () =>
            {
                if (id == 0)
                {
                    await api.PostAsync<TDto>(ApiPath, form);
                }
                else
                {
                    await api.PutAsync<TDto>($"{ApiPath}/{id}", form!);
                }
            }))
        {
            TempData["Success"] = id == 0 ? "Запись добавлена" : "Изменения сохранены";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Id = id;
        if (id != 0)
        {
            ViewBag.Item = await api.GetAsync<TDto>($"{ApiPath}/{id}");
        }
        await PrepareFormAsync();
        return View("Edit", form);
    }

    // hard=false — в корзину, hard=true — навсегда (из корзины)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, bool hard, string? returnUrl)
    {
        await RunAndNotifyAsync(() => api.DeleteAsync($"{ApiPath}/{id}{(hard ? "?hard=true" : "")}"),
            hard ? "Запись удалена навсегда" : "Запись перемещена в корзину");
        return BackTo(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, string? returnUrl)
    {
        await RunAndNotifyAsync(() => api.PostAsync($"{ApiPath}/{id}/restore"), "Запись восстановлена");
        return BackTo(returnUrl);
    }

    // массовое удаление: отмеченные галочками записи по одной отправляю в Api
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMany(int[] ids, bool hard, string? returnUrl)
    {
        if (ids.Length == 0)
        {
            TempData["Error"] = "Ничего не выбрано";
            return BackTo(returnUrl);
        }

        var done = 0;
        var errors = new List<string>();
        foreach (var id in ids)
        {
            try
            {
                await api.DeleteAsync($"{ApiPath}/{id}{(hard ? "?hard=true" : "")}");
                done++;
            }
            catch (ApiException ex)
            {
                errors.Add($"№{id}: {ex.Error.Message}");
            }
        }

        if (done > 0)
        {
            TempData["Success"] = hard ? $"Удалено навсегда: {done}" : $"Перемещено в корзину: {done}";
        }
        if (errors.Count > 0)
        {
            TempData["Error"] = "Не удалось удалить: " + string.Join("; ", errors);
        }
        return BackTo(returnUrl);
    }
}
