using System.Linq.Expressions;

using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;

using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Common;

// общие действия корзины, одинаковые для клиентов, работ, запчастей и пользователей
public static class SoftDeleteExtensions
{
    // обычный список или корзина (?deleted=true)
    public static IQueryable<T> WithTrash<T>(this IQueryable<T> query, bool deleted) where T : class, ISoftDeletable =>
        deleted ? query.IgnoreQueryFilters().Where(x => x.DeletedAt != null) : query;

    public static async Task MoveToTrashAsync<T>(this AppDbContext db, Expression<Func<T, bool>> byId, string what)
        where T : class, ISoftDeletable
    {
        var item = await db.Set<T>().SingleOrDefaultAsync(byId) ?? throw ApiException.NotFound(what);
        item.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public static async Task RestoreAsync<T>(this AppDbContext db, Expression<Func<T, bool>> byId, string what)
        where T : class, ISoftDeletable
    {
        var item = await db.Set<T>().IgnoreQueryFilters().Where(x => x.DeletedAt != null).SingleOrDefaultAsync(byId)
            ?? throw ApiException.NotFound($"{what} в корзине");
        item.DeletedAt = null;
        await db.SaveChangesAsync();
    }

    // удаление навсегда. Если на запись ссылаются, база ответит ошибкой внешнего ключа → 409
    public static async Task DeleteForeverAsync<T>(this AppDbContext db, Expression<Func<T, bool>> byId, string what)
        where T : class, ISoftDeletable
    {
        var deleted = await db.Set<T>().IgnoreQueryFilters().Where(byId).ExecuteDeleteAsync();
        if (deleted == 0)
        {
            throw ApiException.NotFound(what);
        }
    }
}
