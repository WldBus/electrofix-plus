using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;
using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ElectroFix.Api.Services;

// настройки из секции Jwt (Key — только из user-secrets / переменной Jwt__Key)
public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "ElectroFix";
    public string Audience { get; set; } = "ElectroFix";
    public int AccessMinutes { get; set; } = 15;
    public int RefreshDays { get; set; } = 7;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public class AuthService(AppDbContext db, JwtOptions jwt, UsersService users, SecurityLog log)
{
    public async Task<AuthResponse> LoginAsync(LoginRequest r)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        // удалённые в корзину пользователи сюда не попадут из-за фильтра
        var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email);

        // одинаковый ответ и для «нет такого email», и для «не тот пароль», чтобы не подсказывать.
        // Сам факт неудачной попытки пишу в журнал: много таких подряд — подбор пароля
        if (user == null || !BCrypt.Net.BCrypt.Verify(r.Password, user.PasswordHash))
        {
            await log.WriteAsync("LOGIN_FAILED", user?.UserId, objectId: user?.UserId, data: new { email });
            throw new ApiException(401, "Неверный email или пароль");
        }

        // про блокировку говорю только тому, кто знает пароль
        if (user.IsBlocked)
        {
            await log.WriteAsync("LOGIN_BLOCKED", user.UserId, objectId: user.UserId);
            throw new ApiException(403, "Учётная запись заблокирована администратором");
        }

        // в панель администратора пускаю только админа, остальные попытки — в журнал
        if (r.App == "admin" && user.Role!.Code != RoleCodes.Admin)
        {
            await log.WriteAsync("ACCESS_DENIED", user.UserId, objectId: user.UserId, data: new { page = "admin-panel" });
            throw new ApiException(403, "Нет доступа к панели администратора");
        }

        await log.WriteAsync("LOGIN", user.UserId, objectId: user.UserId);
        return await IssueAsync(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest r)
    {
        var clientRole = await db.Roles.SingleAsync(x => x.Code == RoleCodes.Client);
        // роль всегда «Клиент», выбрать себе другую при регистрации нельзя
        var created = await users.CreateAsync(new UserSaveRequest
        {
            RoleId = clientRole.RoleId,
            FullName = r.FullName,
            Email = r.Email,
            Phone = r.Phone,
            Password = r.Password,
        });
        return await LoginAsync(new LoginRequest { Email = created.Email, Password = r.Password });
    }

    // ротация: старый refresh-токен удаляю и выдаю новую пару
    public async Task<AuthResponse> RefreshAsync(RefreshRequest r)
    {
        var hash = Hash(r.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash);
        if (token == null || token.ExpiresAt < DateTime.UtcNow)
        {
            throw new ApiException(401, "Сессия истекла, войдите заново");
        }

        var user = await db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == token.UserId);
        db.RefreshTokens.Remove(token);
        await db.SaveChangesAsync();

        // пользователя успели удалить в корзину или заблокировать — новую пару не даю
        return user == null || user.IsBlocked
            ? throw new ApiException(401, "Учётная запись отключена")
            : await IssueAsync(user);
    }

    public async Task LogoutAsync(RefreshRequest r, int? userId)
    {
        var hash = Hash(r.RefreshToken);
        await db.RefreshTokens.Where(t => t.TokenHash == hash).ExecuteDeleteAsync();
        if (userId != null)
        {
            await log.WriteAsync("LOGOUT", userId, objectId: userId);
        }
    }

    private async Task<AuthResponse> IssueAsync(User user)
    {
        // роль кладу в токен из базы, а не из того, что прислал клиент
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Expires = DateTime.UtcNow.AddMinutes(jwt.AccessMinutes),
            SigningCredentials = new SigningCredentials(jwt.SigningKey(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.UserId.ToString()),
                new Claim("name", user.FullName),
                new Claim("role", user.Role!.Code),
            ]),
        };
        var accessToken = new JsonWebTokenHandler().CreateToken(descriptor);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = Hash(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(jwt.RefreshDays),
        });
        // заодно чищу протухшие токены этого пользователя
        await db.RefreshTokens.Where(t => t.UserId == user.UserId && t.ExpiresAt < DateTime.UtcNow).ExecuteDeleteAsync();
        await db.SaveChangesAsync();

        return new AuthResponse(accessToken, refreshToken, jwt.AccessMinutes * 60, await users.GetAsync(user.UserId));
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
