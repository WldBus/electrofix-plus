using System.ComponentModel.DataAnnotations;

namespace ElectroFix.Shared;

public record RoleDto(short RoleId, string Code, string Name, int UsersCount);

public record UserDto(
    int UserId,
    short RoleId,
    string RoleCode,
    string RoleName,
    [property: PersonalName] string FullName,
    string Email,
    string? Phone,
    DateTime CreatedAt,
    DateTime? DeletedAt,
    bool IsBlocked = false,
    string? BlockReason = null,
    DateTime? LastLoginAt = null);

public class UserSaveRequest
{
    [Range(1, short.MaxValue, ErrorMessage = "Выберите роль")]
    public short RoleId { get; set; }

    [Required(ErrorMessage = "Укажите ФИО")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "ФИО: от 3 до 150 символов")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    [StringLength(120, ErrorMessage = "Email: не больше 120 символов")]
    public string Email { get; set; } = "";

    [RegularExpression(@"^\+7[0-9]{10}$", ErrorMessage = "Телефон в формате +7XXXXXXXXXX")]
    public string? Phone { get; set; }

    // при создании обязателен, при изменении — только если меняем пароль
    [StringLength(64, MinimumLength = 8, ErrorMessage = "Пароль: от 8 до 64 символов")]
    public string? Password { get; set; }
}

public class BlockRequest
{
    [Required(ErrorMessage = "Укажите причину блокировки")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Причина: от 3 до 200 символов")]
    public string Reason { get; set; } = "";
}
