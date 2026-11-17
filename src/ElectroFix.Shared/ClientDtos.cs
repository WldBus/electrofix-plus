using System.ComponentModel.DataAnnotations;

namespace ElectroFix.Shared;

public record ClientDto(
    int ClientId,
    string ClientType,
    [property: PersonalName] string DisplayName,
    string? Inn,
    string? Address,
    string Phone,
    string? Email,
    DateTime CreatedAt,
    int DevicesCount,
    DateTime? DeletedAt);

public class ClientSaveRequest
{
    // тип клиента меняется только при создании (от него зависит ИНН)
    [Required(ErrorMessage = "Выберите тип клиента")]
    [RegularExpression("^(individual|legal_entity|management_company)$", ErrorMessage = "Неизвестный тип клиента")]
    public string ClientType { get; set; } = "individual";

    [Required(ErrorMessage = "Укажите наименование")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Наименование: от 2 до 150 символов")]
    public string DisplayName { get; set; } = "";

    [RegularExpression(@"^([0-9]{10}|[0-9]{12})$", ErrorMessage = "ИНН: 10 или 12 цифр")]
    public string? Inn { get; set; }

    [StringLength(255, ErrorMessage = "Адрес: не больше 255 символов")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Укажите телефон")]
    [RegularExpression(@"^\+7[0-9]{10}$", ErrorMessage = "Телефон в формате +7XXXXXXXXXX")]
    public string Phone { get; set; } = "";

    [EmailAddress(ErrorMessage = "Некорректный email")]
    [StringLength(120, ErrorMessage = "Email: не больше 120 символов")]
    public string? Email { get; set; }
}

public record DeviceDto(
    int DeviceId,
    int ClientId,
    [property: PersonalName] string ClientName,
    string Type,
    string Brand,
    string Model,
    int? ManufactureYear,
    string SerialNumber,
    string? InventoryNumber,
    int RuntimeHours);
