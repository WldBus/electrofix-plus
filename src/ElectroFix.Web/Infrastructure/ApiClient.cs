using System.Net;
using System.Net.Http.Json;

using ElectroFix.Shared;

namespace ElectroFix.Web.Infrastructure;

// ошибка, которую вернул Api (404, 409, 422 ...), с текстом и ошибками полей
public class ApiException(HttpStatusCode status, ErrorResponse error) : Exception(error.Message)
{
    public HttpStatusCode Status { get; } = status;
    public ErrorResponse Error { get; } = error;
}

// сессия кончилась и обновить токен не вышло — нужно заново войти
public class SessionExpiredException() : Exception("Сессия истекла");

// Типизированный клиент основного Api. Токен подставляет TokenHandler
public class ApiClient(HttpClient http)
{
    public async Task<T> GetAsync<T>(string url) =>
        (await ReadAsync<T>(await http.GetAsync(url)))!;

    public async Task<T> PostAsync<T>(string url, object? body) =>
        (await ReadAsync<T>(await http.PostAsJsonAsync(url, body)))!;

    public async Task PostAsync(string url, object? body = null) =>
        await ReadAsync<object>(await http.PostAsJsonAsync(url, body));

    public async Task<T> PutAsync<T>(string url, object body) =>
        (await ReadAsync<T>(await http.PutAsJsonAsync(url, body)))!;

    public async Task DeleteAsync(string url) =>
        await ReadAsync<object>(await http.DeleteAsync(url));

    // все записи списка для выпадающих списков: Api отдаёт максимум 100 за раз, поэтому иду по страницам
    public async Task<List<T>> AllAsync<T>(string url)
    {
        var all = new List<T>();
        var separator = url.Contains('?') ? '&' : '?';
        for (var page = 1; ; page++)
        {
            var result = await GetAsync<PagedResult<T>>($"{url}{separator}size=100&page={page}");
            all.AddRange(result.Items);
            if (all.Count >= result.Total || result.Items.Count == 0)
            {
                return all;
            }
        }
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object)
                ? default
                : await response.Content.ReadFromJsonAsync<T>();
        }

        ErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        }
        catch (Exception)
        {
            // тело не в нашем формате (например, ошибка прокси) — подставлю общий текст
        }
        throw new ApiException(response.StatusCode, error ?? new ErrorResponse($"Сервер ответил {(int)response.StatusCode}"));
    }
}
