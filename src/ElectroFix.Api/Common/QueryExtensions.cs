using System.Linq.Expressions;

using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Common;

public static class QueryExtensions
{
    public const int MaxPageSize = 100;

    // sort=name,asc → OrderBy по полю из словаря. Все поля, по которым можно
    // сортировать, перечислены явно, чтобы из адреса нельзя было подсунуть что угодно
    public static IQueryable<T> SortBy<T>(this IQueryable<T> query, string? sort,
        Dictionary<string, Expression<Func<T, object?>>> fields, string defaultSort)
    {
        var parts = (string.IsNullOrWhiteSpace(sort) ? defaultSort : sort).Split(',', StringSplitOptions.TrimEntries);
        var field = parts[0].ToLowerInvariant();
        var desc = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        if (!fields.TryGetValue(field, out var key))
        {
            throw ApiException.BadRequest($"Нельзя сортировать по «{parts[0]}». Можно: {string.Join(", ", fields.Keys)}");
        }
        return desc ? query.OrderByDescending(key) : query.OrderBy(key);
    }

    // Skip/Take выполняются в SQL (LIMIT/OFFSET), total — отдельный COUNT
    public static async Task<PagedResult<TDto>> ToPagedAsync<T, TDto>(this IQueryable<T> query,
        ListQuery q, Expression<Func<T, TDto>> map)
    {
        var page = Math.Max(q.Page, 1);
        var size = Math.Clamp(q.Size, 1, MaxPageSize);
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * size).Take(size).Select(map).ToListAsync();
        return new PagedResult<TDto>(items, page, size, total);
    }

    // для поиска: «%текст%» без учёта регистра (ILIKE в PostgreSQL)
    public static string? Like(this string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
}
