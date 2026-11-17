using System.Net;
using System.Security.Claims;

using ElectroFix.Admin.Infrastructure;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Admin.Controllers;

public class AccountController(ApiClient api) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
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
            // App = admin: Api сам не пустит не-админа и запишет попытку в журнал аудита
            form.App = "admin";
            auth = await api.PostAsync<AuthResponse>("/api/auth/login", form);
        }
        catch (ApiException ex) when (ex.Status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                                          or HttpStatusCode.UnprocessableEntity)
        {
            ModelState.AddModelError(string.Empty, ex.Error.Message);
            return View(form);
        }

        // роль беру из ответа Api, токены кладу внутрь cookie (она шифруется и недоступна JS)
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
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
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
                // даже если Api не ответил, из панели всё равно выхожу
            }
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // сюда попадаю, если Api ответил 403 на какое-то действие
    [Authorize]
    public IActionResult Denied() => View();
}
