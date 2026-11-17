using System.ComponentModel.DataAnnotations;

namespace ElectroFix.Shared;

public class LoginRequest
{
    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Укажите пароль")]
    public string Password { get; set; } = "";

    // "admin" — вход в панель администратора: Api пустит только роль admin
    public string? App { get; set; }
}

// самостоятельная регистрация — всегда с ролью «Клиент»
public class RegisterRequest
{
    [Required(ErrorMessage = "Укажите ФИО")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "ФИО: от 3 до 150 символов")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Укажите email")]
    [EmailAddress(ErrorMessage = "Некорректный email")]
    [StringLength(120, ErrorMessage = "Email: не больше 120 символов")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Укажите телефон")]
    [RegularExpression(@"^\+7[0-9]{10}$", ErrorMessage = "Телефон в формате +7XXXXXXXXXX")]
    public string Phone { get; set; } = "";

    [Required(ErrorMessage = "Придумайте пароль")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "Пароль: от 8 до 64 символов")]
    public string Password { get; set; } = "";
}

public class RefreshRequest
{
    [Required(ErrorMessage = "Нет refresh-токена")]
    public string RefreshToken { get; set; } = "";
}

// expiresIn — через сколько секунд истечёт accessToken
public record AuthResponse(string AccessToken, string RefreshToken, int ExpiresIn, UserDto User);
