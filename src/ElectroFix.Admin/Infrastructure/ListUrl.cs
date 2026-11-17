using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace ElectroFix.Admin.Infrastructure;

// Помощники для адресов списков: фильтры, сортировка и страница живут в адресе,
// поэтому ссылку на отфильтрованный список можно отправить другому человеку
public static class ListUrl
{
    // текущий адрес страницы с заменой отдельных параметров (?page=2 и т. п.)
    public static string With(HttpRequest request, params (string Key, object? Value)[] changes)
    {
        var query = request.Query.ToDictionary(x => x.Key, x => x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changes)
        {
            var text = value?.ToString();
            if (string.IsNullOrEmpty(text))
            {
                query.Remove(key);
            }
            else
            {
                query[key] = text;
            }
        }
        return QueryHelpers.AddQueryString(request.Path, query.Select(x => new KeyValuePair<string, StringValues>(x.Key, x.Value)));
    }

    // объект с параметрами списка → строка запроса к Api (?search=..&page=..)
    public static string ToApiQuery(object query)
    {
        var parts = new List<KeyValuePair<string, string?>>();
        foreach (var p in query.GetType().GetProperties())
        {
            var value = p.GetValue(query);
            if (value == null || value is false || (value is string s && string.IsNullOrWhiteSpace(s)))
            {
                continue;
            }
            var text = value is bool ? "true" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            parts.Add(new(JsonNamingPolicy.CamelCase.ConvertName(p.Name), text));
        }
        return QueryHelpers.AddQueryString("", parts);
    }
}

// данные для подвала списка: страницы и общее количество
public record PagerModel(int Page, int Size, int Total)
{
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)Size));
}
