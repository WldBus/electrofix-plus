using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using ElectroFix.Shared;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ElectroFix.Admin.Infrastructure;

// Подставляет accessToken из cookie в каждый запрос к Api и сервису статистики.
// Если Api ответил 401 (токен живёт 15 минут), один раз обновляю пару токенов
// через /api/auth/refresh, сохраняю их в cookie и повторяю запрос
public class TokenHandler(IHttpContextAccessor accessor, IHttpClientFactory factory) : DelegatingHandler
{
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var context = accessor.HttpContext;
        // если в этом же запросе токен уже обновляли, беру свежий, а не тот, что пришёл в cookie
        var access = context == null ? null
            : context.Items[AccessToken] as string ?? await context.GetTokenAsync(AccessToken);
        if (access == null)
        {
            // ещё не вошли (например, сам запрос входа) — отправляю как есть
            return await base.SendAsync(request, ct);
        }

        var retry = await CloneAsync(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var response = await base.SendAsync(request, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var newAccess = await RefreshAsync(context!, ct) ?? throw new SessionExpiredException();
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newAccess);
        response.Dispose();
        return await base.SendAsync(retry, ct);
    }

    private async Task<string?> RefreshAsync(HttpContext context, CancellationToken ct)
    {
        var auth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var props = auth.Properties;
        var refresh = props?.GetTokenValue(RefreshToken);
        if (props == null || refresh == null || auth.Principal == null)
        {
            return null;
        }

        // отдельный клиент без этого обработчика, иначе уйдём в рекурсию
        var response = await factory.CreateClient("auth")
            .PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = refresh }, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>(ct))!;
        props.UpdateTokenValue(AccessToken, tokens.AccessToken);
        props.UpdateTokenValue(RefreshToken, tokens.RefreshToken);
        // перезаписываю cookie с новыми токенами
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, auth.Principal, props);
        context.Items[AccessToken] = tokens.AccessToken;
        return tokens.AccessToken;
    }

    // запрос нельзя отправить дважды, поэтому для повтора делаю копию заранее
    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var copy = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content != null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            copy.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                copy.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        foreach (var header in request.Headers)
        {
            copy.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return copy;
    }
}
