using System.Net;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ElectroFix.Admin.Infrastructure;

// Общая обработка того, что не поймал сам контроллер:
// сессия истекла → на вход; Api лежит или ответил ошибкой → страница с «Повторить»
public class ApiErrorFilter(ITempDataDictionaryFactory tempData, IModelMetadataProvider metadata) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var http = context.HttpContext;
        switch (context.Exception)
        {
            case SessionExpiredException:
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                tempData.GetTempData(http)["Error"] = "Сессия истекла, войдите заново";
                context.Result = new RedirectToActionResult("Login", "Account",
                    new { returnUrl = http.Request.Path + http.Request.QueryString });
                break;

            case ApiException { Status: HttpStatusCode.Forbidden }:
                context.Result = new RedirectToActionResult("Denied", "Account", null);
                break;

            case ApiException api:
                context.Result = ErrorView(context, api.Error.Message, (int)api.Status);
                break;

            case HttpRequestException or TaskCanceledException:
                // это и есть состояние «ошибка»: Api недоступен (на бесплатном Render он может просыпаться)
                context.Result = ErrorView(context,
                    "Сервер данных не отвечает. Возможно, он просыпается после простоя — подождите минуту и повторите.", 503);
                break;

            default:
                return;
        }
        context.ExceptionHandled = true;
    }

    private ViewResult ErrorView(ExceptionContext context, string message, int status) => new()
    {
        ViewName = "ApiError",
        StatusCode = status == 404 ? 404 : 503,
        ViewData = new ViewDataDictionary(metadata, context.ModelState)
        {
            ["Title"] = status == 404 ? "Не найдено" : "Ошибка",
            ["Message"] = message,
            ["RetryUrl"] = context.HttpContext.Request.Method == "GET"
                ? context.HttpContext.Request.Path + context.HttpContext.Request.QueryString
                : null,
        },
    };
}
