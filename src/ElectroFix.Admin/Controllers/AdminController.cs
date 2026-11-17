using System.Net;

using ElectroFix.Admin.Infrastructure;

using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Admin.Controllers;

// Общий предок контроллеров панели. Пускает только роль admin (политика по умолчанию в Program.cs)
public abstract class AdminController : Controller
{
    // Выполняет запрос к Api из формы. Ошибки полей (422) раскладываю под поля формы,
    // остальное (409, 400, 404) показываю сверху формы. Возвращает true, если всё прошло
    protected async Task<bool> TryApiAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (ApiException ex) when (ex.Status is HttpStatusCode.UnprocessableEntity or HttpStatusCode.Conflict
                                          or HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
        {
            if (ex.Error.Errors is { Count: > 0 } errors)
            {
                foreach (var (field, message) in errors)
                {
                    // Api отдаёт имена полей в camelCase, а в форме они в PascalCase
                    ModelState.AddModelError(char.ToUpperInvariant(field[0]) + field[1..], message);
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, ex.Error.Message);
            }
            return false;
        }
    }

    // То же для кнопок в списке (удалить, восстановить): результат показываю над таблицей
    protected async Task RunAndNotifyAsync(Func<Task> action, string success)
    {
        if (await TryApiAsync(action))
        {
            TempData["Success"] = success;
        }
        else
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
        }
    }

    // после действия возвращаюсь туда, откуда пришёл (с теми же фильтрами и страницей)
    protected IActionResult BackTo(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index");
}
