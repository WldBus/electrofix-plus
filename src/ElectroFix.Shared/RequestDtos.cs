using System.ComponentModel.DataAnnotations;

namespace ElectroFix.Shared;

// Заявка на ремонт с сайта. Оставить её можно без регистрации: приёмщик перезванивает
// и сам заводит клиента и запись на приём
public class RepairRequestSaveRequest
{
    [Required(ErrorMessage = "Укажите, что за техника")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Тип техники: от 2 до 50 символов")]
    public string DeviceType { get; set; } = "";

    [Required(ErrorMessage = "Укажите марку и модель")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Марка и модель: от 2 до 100 символов")]
    public string DeviceModel { get; set; } = "";

    [Required(ErrorMessage = "Опишите, что случилось")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Описание: от 5 до 1000 символов")]
    public string Problem { get; set; } = "";

    [Required(ErrorMessage = "Выберите, как передадите технику")]
    [RegularExpression("^(self|courier|master_visit)$", ErrorMessage = "Неизвестный способ передачи")]
    public string DeliveryMethod { get; set; } = "self";

    // филиал нужен, если клиент принесёт сам; адрес — если курьер или выезд мастера.
    // Какое из двух обязательно, проверяю в Api (RequestsService.CheckDelivery)
    [StringLength(100)]
    public string? Branch { get; set; }

    [StringLength(255, ErrorMessage = "Адрес: не больше 255 символов")]
    public string? Address { get; set; }

    // работа, выбранная в прайсе (необязательно)
    public int? ServiceId { get; set; }

    [Required(ErrorMessage = "Как к вам обращаться?")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Имя: от 2 до 150 символов")]
    public string ClientName { get; set; } = "";

    [Required(ErrorMessage = "Укажите телефон")]
    [RegularExpression(@"^\+7[0-9]{10}$", ErrorMessage = "Телефон в формате +7XXXXXXXXXX")]
    public string Phone { get; set; } = "";

    [EmailAddress(ErrorMessage = "Некорректный email")]
    [StringLength(120, ErrorMessage = "Email: не больше 120 символов")]
    public string? Email { get; set; }
}

public record RepairRequestDto(
    int RequestId,
    string DeviceType,
    string DeviceModel,
    string Problem,
    string DeliveryMethod,
    string? Branch,
    string? Address,
    int? ServiceId,
    string? ServiceName,
    [property: PersonalName] string ClientName,
    string Phone,
    string? Email,
    string Status,
    string? Comment,
    DateTime CreatedAt,
    DateTime? ProcessedAt,
    string? ProcessedBy);

public class RequestStatusChange
{
    [Required(ErrorMessage = "Выберите статус")]
    [RegularExpression("^(new|processing|done|rejected)$", ErrorMessage = "Неизвестный статус заявки")]
    public string Status { get; set; } = "";

    [StringLength(255, ErrorMessage = "Комментарий: не больше 255 символов")]
    public string? Comment { get; set; }
}

// Проверка статуса ремонта на сайте без входа: номер квитанции + последние 4 цифры телефона.
// Без цифр телефона любой мог бы перебрать номера по порядку и смотреть чужие ремонты
public class RepairStatusQuery
{
    [Required(ErrorMessage = "Введите номер квитанции")]
    [RegularExpression(@"^[\sЗНзнZNzn-]*\d{4}[\s-]*\d{1,5}\s*$", ErrorMessage = "Номер квитанции выглядит так: ЗН-2026-00012")]
    public string Number { get; set; } = "";

    [Required(ErrorMessage = "Введите 4 последние цифры телефона")]
    [RegularExpression("^[0-9]{4}$", ErrorMessage = "Нужны 4 цифры")]
    public string Phone { get; set; } = "";
}

// наружу отдаю только этап и устройство: без ФИО, сумм и мастера
public record RepairStatusDto(
    string Number,
    string Status,
    string Device,
    DateTime OpenedAt,
    DateTime? ClosedAt);

public static class RepairStages
{
    // этапы ремонта в том порядке, как их видит клиент (статусы из work_orders)
    public static readonly (string Status, string Name)[] Public =
    [
        ("diagnostics", "Диагностика"),
        ("estimate", "Смета"),
        ("approved", "Согласование"),
        ("in_progress", "Ремонт"),
        ("pdi", "Проверка"),
        ("ready", "Готов к выдаче"),
        ("closed", "Выдан"),
    ];

    // на каком этапе сейчас заказ: «new» — это ещё диагностика, «assigned» — уже ремонт
    public static int IndexOf(string status) => status switch
    {
        "new" => 0,
        "assigned" => 3,
        _ => Array.FindIndex(Public, s => s.Status == status),
    };

    // ЗН-2026-12 / зн 2026 00012 / 2026-00012 → ЗН-2026-00012
    public static string? Normalize(string? number)
    {
        var digits = new string((number ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.Length < 5 || digits.Length > 9)
        {
            return null;
        }
        return $"ЗН-{digits[..4]}-{digits[4..].PadLeft(5, '0')}";
    }
}

// группы прайса на сайте (категории services). «Общие» — диагностика, она нужна любой технике
public static class ServiceGroups
{
    public const string Common = "Общие";

    public static readonly string[] All =
        ["Смартфоны", "Ноутбуки и ПК", "Крупная бытовая техника", "Мелкая бытовая техника"];
}
