using System.Linq.Expressions;

using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;
using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Services;

// Учётные записи сотрудников и клиентов. Пароль храню только как bcrypt-хэш
public class UsersService(AppDbContext db, CurrentUser current, SecurityLog log)
{
    // последний вход беру из журнала аудита
    private Expression<Func<User, UserDto>> ToDto => u => new UserDto(
        u.UserId, u.RoleId, u.Role!.Code, u.Role.Name, u.FullName, u.Email, u.Phone, u.CreatedAt, u.DeletedAt,
        u.IsBlocked, u.BlockReason,
        db.AuditLog.Where(a => a.UserId == u.UserId && a.Action == "LOGIN").Max(a => (DateTime?)a.CreatedAt));

    private static readonly Dictionary<string, Expression<Func<User, object?>>> SortFields = new()
    {
        ["id"] = u => u.UserId,
        ["name"] = u => u.FullName,
        ["email"] = u => u.Email,
        ["role"] = u => u.RoleId,
        ["created"] = u => u.CreatedAt,
    };

    public Task<List<RoleDto>> RolesAsync() =>
        db.Roles.OrderBy(r => r.RoleId)
            .Select(r => new RoleDto(r.RoleId, r.Code, r.Name, db.Users.Count(u => u.RoleId == r.RoleId)))
            .ToListAsync();

    public Task<PagedResult<UserDto>> ListAsync(UserListQuery q)
    {
        var query = db.Users.WithTrash(q.Deleted);
        if (q.Search.Like() is { } like)
        {
            query = query.Where(u => EF.Functions.ILike(u.FullName, like) || EF.Functions.ILike(u.Email, like));
        }
        if (q.RoleId != null)
        {
            query = query.Where(u => u.RoleId == q.RoleId);
        }
        if (q.Blocked)
        {
            query = query.Where(u => u.IsBlocked);
        }
        return query.SortBy(q.Sort, SortFields, "role").ToPagedAsync(q, ToDto);
    }

    public async Task<UserDto> GetAsync(int id) =>
        await db.Users.IgnoreQueryFilters().Where(u => u.UserId == id).Select(ToDto).SingleOrDefaultAsync()
        ?? throw ApiException.NotFound("Пользователь");

    public async Task<UserDto> CreateAsync(UserSaveRequest r)
    {
        if (string.IsNullOrEmpty(r.Password))
        {
            throw ApiException.Field(nameof(r.Password), "Задайте пароль для нового пользователя");
        }
        await CheckRoleAsync(r.RoleId);

        var u = new User { PasswordHash = HashPassword(r.Password) };
        Fill(u, r);
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return await GetAsync(u.UserId);
    }

    public async Task<UserDto> UpdateAsync(int id, UserSaveRequest r)
    {
        var u = await db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.UserId == id)
                ?? throw ApiException.NotFound("Пользователь");
        await CheckRoleAsync(r.RoleId);

        if (id == current.Id && u.RoleId != r.RoleId)
        {
            throw ApiException.Field(nameof(r.RoleId), "Нельзя поменять роль самому себе");
        }
        // нельзя снять роль с последнего администратора, иначе в систему никто не войдёт админом
        if (u.Role!.Code == RoleCodes.Admin && u.RoleId != r.RoleId && await IsLastAdminAsync(id))
        {
            throw ApiException.Field(nameof(r.RoleId), "Это последний администратор, роль снять нельзя");
        }

        // админ видит ФИО скрытым («И****в А. А.»). Если он его не трогал, настоящее ФИО оставляю
        var realName = u.FullName;
        Fill(u, r);
        if (r.FullName.Trim() == PersonalData.MaskName(realName))
        {
            u.FullName = realName;
        }
        if (!string.IsNullOrEmpty(r.Password))
        {
            u.PasswordHash = HashPassword(r.Password);
        }
        await db.SaveChangesAsync();
        return await GetAsync(id);
    }

    public async Task TrashAsync(int id)
    {
        CheckNotSelf(id);
        if (await IsLastAdminAsync(id))
        {
            throw ApiException.Conflict("Это последний администратор, удалить его нельзя");
        }
        await db.MoveToTrashAsync<User>(u => u.UserId == id, "Пользователь");
    }

    public Task RestoreAsync(int id) => db.RestoreAsync<User>(u => u.UserId == id, "Пользователь");

    public async Task DeleteForeverAsync(int id)
    {
        CheckNotSelf(id);
        if (await IsLastAdminAsync(id))
        {
            throw ApiException.Conflict("Это последний администратор, удалить его нельзя");
        }
        await db.DeleteForeverAsync<User>(u => u.UserId == id, "Пользователь");
    }

    // блокировка: войти нельзя, все сессии пользователя обрываю (refresh-токены удаляю,
    // а действующий access-токен перестанет приниматься на следующем же запросе)
    public async Task BlockAsync(int id, string reason)
    {
        if (id == current.Id)
        {
            throw ApiException.Conflict("Нельзя заблокировать самого себя");
        }
        if (await IsLastAdminAsync(id))
        {
            throw ApiException.Conflict("Это последний администратор, блокировать его нельзя");
        }
        var u = await db.Users.SingleOrDefaultAsync(x => x.UserId == id) ?? throw ApiException.NotFound("Пользователь");
        if (u.IsBlocked)
        {
            throw ApiException.Conflict("Пользователь уже заблокирован");
        }

        u.IsBlocked = true;
        u.BlockedAt = DateTime.UtcNow;
        u.BlockReason = reason.Trim();
        await db.SaveChangesAsync();
        await db.RefreshTokens.Where(t => t.UserId == id).ExecuteDeleteAsync();
        await log.WriteAsync("BLOCK", current.Id, objectId: id, data: new { reason = u.BlockReason });
    }

    public async Task UnblockAsync(int id)
    {
        var u = await db.Users.SingleOrDefaultAsync(x => x.UserId == id) ?? throw ApiException.NotFound("Пользователь");
        if (!u.IsBlocked)
        {
            throw ApiException.Conflict("Пользователь не заблокирован");
        }

        u.IsBlocked = false;
        u.BlockedAt = null;
        u.BlockReason = null;
        await db.SaveChangesAsync();
        await log.WriteAsync("UNBLOCK", current.Id, objectId: id);
    }

    public static string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password, 12);

    // пользователь — админ, и других активных админов нет
    private async Task<bool> IsLastAdminAsync(int userId)
    {
        var admins = await db.Users.Where(u => u.Role!.Code == RoleCodes.Admin).Select(u => u.UserId).ToListAsync();
        return admins.Count == 1 && admins[0] == userId;
    }

    private void CheckNotSelf(int id)
    {
        if (id == current.Id)
        {
            throw ApiException.Conflict("Нельзя удалить собственную учётную запись");
        }
    }

    private async Task CheckRoleAsync(short roleId)
    {
        if (!await db.Roles.AnyAsync(r => r.RoleId == roleId))
        {
            throw ApiException.Field(nameof(UserSaveRequest.RoleId), "Такой роли нет");
        }
    }

    private static void Fill(User u, UserSaveRequest r)
    {
        u.RoleId = r.RoleId;
        u.FullName = r.FullName.Trim();
        u.Email = r.Email.Trim().ToLowerInvariant();
        u.Phone = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone;
    }
}
