using System.Net;
using System.Security.Claims;

using ElectroFix.Shared;
using ElectroFix.Web.Infrastructure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Web.Controllers;

// Вход, регистрация клиента и выход. Пароль проверяет Api, сайт только хранит токены в cookie
public class AccountController(ApiClient api) : SiteController
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        // уже вошёл (например, нажал «Войти» в закладках) — сразу на своё рабочее место
        if (User.Identity?.IsAuthenticated == true && returnUrl == null)
        {
            return StartPage(Role);
        }
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginRequest());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest form, string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        AuthResponse auth;
        try
        {
            form.App = null;
            auth = await api.PostAsync<AuthResponse>("/api/auth/login", form);
        }
        catch (ApiException ex) when (ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                                          or HttpStatusCode.UnprocessableEntity)
        {
            ModelState.AddModelError(string.Empty, ex.Error.Message);
            return View(form);
        }

        await SignInAsync(auth);
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : StartPage(auth.User.RoleCode);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() => View(new RegisterRequest());

    // регистрируется только клиент; сотрудников заводит админ в панели
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequest form)
    {
        if (!ModelState.IsValid)
        {
            return View(form);
        }

        AuthResponse? auth = null;
        if (!await TryApiAsync(async () => auth = await api.PostAsync<AuthResponse>("/api/auth/register", form)))
        {
            return View(form);
        }

        await SignInAsync(auth!);
        TempData["Success"] = "Кабинет создан. Добавьте устройство и запишитесь на ремонт";
        return RedirectToAction("Index", "Cabinet");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var refresh = await HttpContext.GetTokenAsync(TokenHandler.RefreshToken);
        if (refresh != null)
        {
            try
            {
                // Api удалит refresh-токен и запишет выход в журнал аудита
                await api.PostAsync("/api/auth/logout", new RefreshRequest { RefreshToken = refresh });
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or SessionExpiredException)
            {
                // даже если Api не ответил, с сайта всё равно выхожу
            }
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // сюда попадаю, если страница не для этой роли или Api ответил 403
    [AllowAnonymous]
    public IActionResult Denied() => View();

    // роль беру из ответа Api, токены кладу внутрь cookie (она шифруется и недоступна JS)
    private async Task SignInAsync(AuthResponse auth)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, auth.User.UserId.ToString()),
            new Claim(ClaimTypes.Name, auth.User.FullName),
            new Claim(ClaimTypes.Role, auth.User.RoleCode),
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        var props = new AuthenticationProperties();
        props.StoreTokens(
        [
            new AuthenticationToken { Name = TokenHandler.AccessToken, Value = auth.AccessToken },
            new AuthenticationToken { Name = TokenHandler.RefreshToken, Value = auth.RefreshToken },
        ]);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), props);
    }

    // после входа каждый попадает на своё рабочее место
    private IActionResult StartPage(string role)
    {
        switch (role)
        {
            case RoleCodes.Client: return RedirectToAction("Index", "Cabinet");
            case RoleCodes.Receptionist: return RedirectToAction("Index", "Reception");
            case RoleCodes.Master: return RedirectToAction("Index", "Master");
            case RoleCodes.Purchaser: return RedirectToAction("Index", "Warehouse");
            default:
                TempData["Info"] = "Администратор БД работает в отдельной админ-панели. На сайте для вас открыт только прайс.";
                return RedirectToAction("Index", "Home");
        }
    }
}
