using System.Net;
using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Web.Infrastructure;

// Общий предок контроллеров сайта: вызов Api из формы и сообщения об успехе/ошибке
public abstract class SiteController : Controller
{
    // id пользователя из cookie (его положил туда AccountController после входа)
    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected string Role => User.FindFirstValue(ClaimTypes.Role) ?? "";

    // Выполняет запрос к Api из формы. Ошибки полей (422) раскладываю под поля формы,
    // остальное (409, 400, 404) показываю сверху формы. prefix — если поля формы вложены (Checkin.DeviceId)
    protected async Task<bool> TryApiAsync(Func<Task> action, string? prefix = null)
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
                    var name = char.ToUpperInvariant(field[0]) + field[1..];
                    ModelState.AddModelError(prefix == null ? name : $"{prefix}.{name}", message);
                }
            }
            else
            {
                ModelState.AddModelError(string.Empty, ex.Error.Message);
            }
            return false;
        }
    }

    // для кнопок на странице (сменить статус, удалить строку): результат показываю сверху страницы
    protected async Task RunAndNotifyAsync(Func<Task> action, string success)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = FirstError();
            return;
        }
        if (await TryApiAsync(action))
        {
            TempData["Success"] = success;
        }
        else
        {
            TempData["Error"] = FirstError();
        }
    }

    private string FirstError() => ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
}
