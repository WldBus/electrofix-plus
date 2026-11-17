using ElectroFix.Shared;

namespace ElectroFix.Api.Common;

// Кто сейчас делает запрос: id и роль берутся из JWT (claims sub и role)
public class CurrentUser(IHttpContextAccessor accessor)
{
    public int? Id => int.TryParse(accessor.HttpContext?.User.FindFirst("sub")?.Value, out var id) ? id : null;

    public string? Role => accessor.HttpContext?.User.FindFirst("role")?.Value;

    public bool IsAdmin => Role == RoleCodes.Admin;
}

// наборы ролей для [Authorize(Roles = ...)]
public static class Roles
{
    public const string Admin = RoleCodes.Admin;
    public const string Staff = "admin,receptionist,purchaser,master";
    public const string AdminOrReceptionist = "admin,receptionist";
    public const string AdminOrPurchaser = "admin,purchaser";
}
