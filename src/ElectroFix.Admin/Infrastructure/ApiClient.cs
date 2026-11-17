using System.Net;
using System.Net.Http.Json;

using ElectroFix.Shared;

namespace ElectroFix.Admin.Infrastructure;

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

// клиент сервиса статистики
public class StatsClient(HttpClient http)
{
    public async Task<DashboardDto> DashboardAsync(int months = 12)
    {
        var response = await http.GetAsync($"/api/stats/dashboard?months={months}");
        if (!response.IsSuccessStatusCode)
        {
            throw new ApiException(response.StatusCode, new ErrorResponse("Сервис статистики не ответил"));
        }
        return (await response.Content.ReadFromJsonAsync<DashboardDto>())!;
    }
}
