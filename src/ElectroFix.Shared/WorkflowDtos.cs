using System.ComponentModel.DataAnnotations;

namespace ElectroFix.Shared;

// Запросы рабочих операций: запись на приём, смета, оплата, склад.
// Ими пользуются сайт клиента и рабочие места сотрудников

public static class Branches
{
    public static readonly string[] All = ["Москва, Тверская", "Москва, Люблино"];
}

public class DeviceSaveRequest
{
    // клиенту не нужен (устройство всегда его), приёмщик выбирает клиента сам
    public int ClientId { get; set; }

    [Required(ErrorMessage = "Укажите тип устройства")]
    [StringLength(50, ErrorMessage = "Тип: не больше 50 символов")]
    public string Type { get; set; } = "";

    [Required(ErrorMessage = "Укажите марку")]
    [StringLength(80, ErrorMessage = "Марка: не больше 80 символов")]
    public string Brand { get; set; } = "";

    [Required(ErrorMessage = "Укажите модель")]
    [StringLength(100, ErrorMessage = "Модель: не больше 100 символов")]
    public string Model { get; set; } = "";

    [Range(1950, 2100, ErrorMessage = "Год выпуска: от 1950 до 2100")]
    public int? ManufactureYear { get; set; }

    [Required(ErrorMessage = "Укажите серийный номер")]
    [RegularExpression("^[A-Za-z0-9/-]{4,50}$", ErrorMessage = "Серийный номер: 4–50 символов, латиница, цифры, / и -")]
    public string SerialNumber { get; set; } = "";

    [StringLength(50, ErrorMessage = "Инвентарный номер: не больше 50 символов")]
    public string? InventoryNumber { get; set; }
}

// запись на приём: время — по Москве, как его ввели в форме
public class CheckinSaveRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Выберите устройство")]
    public int DeviceId { get; set; }

    [Required(ErrorMessage = "Выберите филиал")]
    public string Branch { get; set; } = Branches.All[0];

    [Required(ErrorMessage = "Укажите дату и время")]
    public DateTime? SlotStart { get; set; }

    [Range(30, 240, ErrorMessage = "Длительность: от 30 до 240 минут")]
    public int DurationMinutes { get; set; } = 60;

    [StringLength(255, ErrorMessage = "Не больше 255 символов")]
    public string? ServicesRequested { get; set; }

    [Required(ErrorMessage = "Опишите, что случилось")]
    [StringLength(1000, ErrorMessage = "Не больше 1000 символов")]
    public string Defects { get; set; } = "";

    [RegularExpression("^(low|normal|high|urgent)$", ErrorMessage = "Неизвестный приоритет")]
    public string Priority { get; set; } = "normal";
}

public class StatusRequest
{
    [Required(ErrorMessage = "Укажите статус")]
    public string Status { get; set; } = "";
}

// приёмка устройства: что видно при осмотре
public class OpenWorkOrderRequest
{
    [StringLength(255, ErrorMessage = "Не больше 255 символов")]
    public string? ConditionState { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Наработка не может быть отрицательной")]
    public int? RuntimeAtCheckin { get; set; }
}

public class AssignMasterRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Выберите мастера")]
    public int MasterId { get; set; }
}

public class OrderServiceAddRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Выберите работу")]
    public int ServiceId { get; set; }

    [Range(-50, 100, ErrorMessage = "Скидка/наценка: от −50 до 100 %")]
    public decimal DiscountMarkup { get; set; }
}

public class OrderPartAddRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Выберите запчасть")]
    public int PartId { get; set; }

    [Range(0.01, 10_000, ErrorMessage = "Количество: от 0,01 до 10 000")]
    public decimal Quantity { get; set; } = 1;

    [Range(0, 10_000_000, ErrorMessage = "Цена: от 0 до 10 000 000")]
    public decimal Price { get; set; }
}

public class ActualTimeRequest
{
    [Range(0, 200, ErrorMessage = "Время: от 0 до 200 ч")]
    public decimal ActualTime { get; set; }
}

// решение по смете: отмеченные позиции согласованы, остальные «на согласовании» — отклонены
public class EstimateDecisionRequest
{
    public int[] ApprovedServiceIds { get; set; } = [];
    public int[] ApprovedPartIds { get; set; } = [];
}

public class PaymentSaveRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Выберите счёт")]
    public int InvoiceId { get; set; }

    [Range(0.01, 10_000_000, ErrorMessage = "Сумма: от 0,01 до 10 000 000")]
    public decimal Amount { get; set; }

    [RegularExpression("^(cash|online|bank_transfer)$", ErrorMessage = "Неизвестный способ оплаты")]
    public string Method { get; set; } = "cash";

    public bool IsRefund { get; set; }
}

public class StockReceiptRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Выберите запчасть")]
    public int PartId { get; set; }

    [Range(0.01, 100_000, ErrorMessage = "Количество: от 0,01 до 100 000")]
    public decimal Quantity { get; set; } = 1;

    [Required(ErrorMessage = "Укажите партию")]
    [StringLength(50, ErrorMessage = "Партия: не больше 50 символов")]
    public string Batch { get; set; } = "";

    [Required(ErrorMessage = "Укажите ячейку склада")]
    [RegularExpression("^[A-Z]-[0-9]{2}-[0-9]{2}$", ErrorMessage = "Ячейка в формате A-01-01 (стеллаж-полка-место)")]
    public string StorageCell { get; set; } = "";
}

public class StockIssueRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Выберите заказ-наряд")]
    public int WorkOrderId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Выберите запчасть")]
    public int PartId { get; set; }

    [Required(ErrorMessage = "Выберите партию")]
    public string Batch { get; set; } = "";

    [Range(0.01, 100_000, ErrorMessage = "Количество: от 0,01 до 100 000")]
    public decimal Quantity { get; set; } = 1;
}

// согласованная запчасть, которую ещё не выдали мастеру
public record PartToIssueDto(
    int WorkOrderId,
    string WorkOrderNumber,
    string WorkOrderStatus,
    int PartId,
    string Article,
    string PartName,
    string Unit,
    decimal Needed,
    decimal Issued)
{
    public decimal Remaining => Needed - Issued;
}

public record MasterDto(int UserId, [property: PersonalName] string FullName, int ActiveOrders);

// Кто и куда может перевести заказ-наряд вручную. Этим пользуется Api для проверки
// и сайт, чтобы показывать только нужные кнопки. Остальные переходы идут через
// отдельные действия: смета, согласование, назначение мастера, закрытие
public static class WorkOrderRules
{
    public static readonly Dictionary<string, string[]> ReceptionistMoves = new()
    {
        ["new"] = ["diagnostics", "cancelled"],
        ["diagnostics"] = ["cancelled"],
        ["estimate"] = ["diagnostics", "cancelled"],
        ["approved"] = ["cancelled"],
        ["pdi"] = ["ready", "in_progress"],
        ["ready"] = ["in_progress"],
    };

    public static readonly Dictionary<string, string[]> MasterMoves = new()
    {
        ["assigned"] = ["in_progress"],
        ["in_progress"] = ["pdi"],
    };

    // смету можно править, пока ремонт не дошёл до проверки качества
    public static readonly string[] EditableEstimate = ["new", "diagnostics", "estimate", "approved", "assigned", "in_progress"];

    // клиенту, закупщику и админу статусы кнопками менять нельзя — для них пусто
    public static string[] MovesFor(string role, string status) => role switch
    {
        RoleCodes.Receptionist => ReceptionistMoves.GetValueOrDefault(status, []),
        RoleCodes.Master => MasterMoves.GetValueOrDefault(status, []),
        _ => [],
    };
}
