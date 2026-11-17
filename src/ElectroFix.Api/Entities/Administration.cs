namespace ElectroFix.Api.Entities;

// роли: admin, receptionist, master, purchaser, client
public class Role
{
    public short RoleId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class User : ISoftDeletable
{
    public int UserId { get; set; }
    public short RoleId { get; set; }
    public Role? Role { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = "";
    public string Settings { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    // блокировка: учётка остаётся в списке, но войти по ней нельзя
    public bool IsBlocked { get; set; }
    public DateTime? BlockedAt { get; set; }
    public string? BlockReason { get; set; }
}

// запись журнала аудита (таблица audit_log), её пишут триггеры БД и вход/выход
public class AuditEntry
{
    public int AuditId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Action { get; set; } = "";
    public string ObjectTable { get; set; } = "";
    public int? ObjectId { get; set; }
    public string? BeforeData { get; set; }
    public string? AfterData { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
