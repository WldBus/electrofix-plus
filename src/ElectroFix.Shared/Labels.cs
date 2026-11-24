namespace ElectroFix.Shared;

// Русские подписи для кодов, которые хранятся в базе (статусы, типы и т. д.)
public static class Labels
{
    public static readonly Dictionary<string, string> ClientTypes = new()
    {
        ["individual"] = "Физическое лицо",
        ["legal_entity"] = "Юридическое лицо",
        ["management_company"] = "Управляющая компания",
    };

    public static readonly Dictionary<string, string> WorkOrderStatuses = new()
    {
        ["new"] = "Новый",
        ["diagnostics"] = "Диагностика",
        ["estimate"] = "Смета",
        ["approved"] = "Смета согласована",
        ["assigned"] = "Мастер назначен",
        ["in_progress"] = "В работе",
        ["pdi"] = "Контроль качества",
        ["ready"] = "Готов к выдаче",
        ["closed"] = "Закрыт",
        ["cancelled"] = "Отменён",
    };

    public static readonly Dictionary<string, string> InvoiceStatuses = new()
    {
        ["unpaid"] = "Не оплачен",
        ["partially_paid"] = "Оплачен частично",
        ["paid"] = "Оплачен",
        ["cancelled"] = "Аннулирован",
    };

    public static readonly Dictionary<string, string> CheckinStatuses = new()
    {
        ["booked"] = "Записан",
        ["confirmed"] = "Подтверждён",
        ["checked_in"] = "Принят",
        ["cancelled"] = "Отменён",
        ["no_show"] = "Неявка",
    };

    public static readonly Dictionary<string, string> Priorities = new()
    {
        ["low"] = "Низкий",
        ["normal"] = "Обычный",
        ["high"] = "Высокий",
        ["urgent"] = "Срочный",
    };

    public static readonly Dictionary<string, string> ApprovalStatuses = new()
    {
        ["draft"] = "Черновик",
        ["pending"] = "На согласовании",
        ["approved"] = "Согласовано",
        ["rejected"] = "Отклонено",
    };

    public static readonly Dictionary<string, string> PaymentMethods = new()
    {
        ["cash"] = "Наличные",
        ["online"] = "Онлайн",
        ["bank_transfer"] = "Безналичный перевод",
    };

    public static readonly Dictionary<string, string> WarrantyDecisions = new()
    {
        ["pending"] = "На рассмотрении",
        ["free_repair"] = "Бесплатный ремонт",
        ["refusal"] = "Отказ",
    };

    public static readonly Dictionary<string, string> MoveTypes = new()
    {
        ["receipt"] = "Поступление",
        ["reserve"] = "Резерв",
        ["issue"] = "Выдача в работу",
        ["return"] = "Возврат",
        ["write_off"] = "Списание",
    };

    public static readonly Dictionary<string, string> RequestStatuses = new()
    {
        ["new"] = "Новая",
        ["processing"] = "В работе",
        ["done"] = "Оформлена",
        ["rejected"] = "Отклонена",
    };

    public static readonly Dictionary<string, string> DeliveryMethods = new()
    {
        ["self"] = "Принесу сам",
        ["courier"] = "Курьер заберёт",
        ["master_visit"] = "Выезд мастера",
    };

    public static readonly Dictionary<string, string> AuditActions = new()
    {
        ["INSERT"] = "Добавление",
        ["UPDATE"] = "Изменение",
        ["DELETE"] = "Удаление",
        ["LOGIN"] = "Вход",
        ["LOGOUT"] = "Выход",
        ["EXPORT"] = "Экспорт",
        ["IMPORT"] = "Импорт",
        ["BACKUP"] = "Резервная копия",
        ["RESTORE"] = "Восстановление",
        ["LOGIN_FAILED"] = "Неудачный вход",
        ["LOGIN_BLOCKED"] = "Вход заблокированного",
        ["UNAUTHORIZED"] = "Запрос без входа",
        ["ACCESS_DENIED"] = "Нет прав",
        ["NOT_FOUND"] = "Нет такой страницы",
        ["BLOCK"] = "Блокировка",
        ["UNBLOCK"] = "Разблокировка",
    };

    // таблицы, которые встречаются в журнале аудита
    public static readonly Dictionary<string, string> AuditTables = new()
    {
        ["users"] = "Пользователи",
        ["clients"] = "Клиенты",
        ["work_orders"] = "Заказ-наряды",
        ["invoices"] = "Счета",
        ["payments"] = "Платежи",
        ["stock_moves"] = "Движения склада",
        ["repair_requests"] = "Заявки с сайта",
        ["http"] = "Запросы к Api",
    };

    // события безопасности: в журнале подсвечиваю их отдельно
    public static readonly HashSet<string> SecurityActions =
        ["LOGIN_FAILED", "LOGIN_BLOCKED", "UNAUTHORIZED", "ACCESS_DENIED", "NOT_FOUND", "BLOCK"];

    public static string Of(Dictionary<string, string> dict, string? code) =>
        code != null && dict.TryGetValue(code, out var label) ? label : code ?? "–";
}
