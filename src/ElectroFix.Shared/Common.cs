namespace ElectroFix.Shared;

// одна страница списка: сами записи и сколько их всего
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int Size, int Total);

// единый формат ошибки Api. errors заполняю только для 422 (поле → текст)
public record ErrorResponse(string Message, Dictionary<string, string>? Errors = null);

// общие параметры списка: ?search=&sort=name,asc&page=1&size=10&deleted=false
public class ListQuery
{
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 10;

    // true — показать корзину (удалённые записи)
    public bool Deleted { get; set; }
}

// фильтры конкретных списков (приходят из адреса вместе с общими параметрами)
public class ClientListQuery : ListQuery
{
    public string? ClientType { get; set; }
}

public class CatalogListQuery : ListQuery
{
    public string? Category { get; set; }
}

public class UserListQuery : ListQuery
{
    public short? RoleId { get; set; }

    // только заблокированные
    public bool Blocked { get; set; }
}

public class StatusListQuery : ListQuery
{
    public string? Status { get; set; }
}

public class WorkOrderListQuery : StatusListQuery
{
    public int? MasterId { get; set; }
    public int? ClientId { get; set; }
}

public class StockMoveListQuery : ListQuery
{
    public string? MoveType { get; set; }
    public int? PartId { get; set; }
}

public class AuditListQuery : ListQuery
{
    public string? Table { get; set; }
    public string? Action { get; set; }

    // email или имя пользователя
    public string? User { get; set; }

    public string? Ip { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    // только события безопасности (неудачные входы, без прав, 404, блокировки)
    public bool Security { get; set; }
}

// коды ролей из таблицы roles
public static class RoleCodes
{
    public const string Admin = "admin";
    public const string Receptionist = "receptionist";
    public const string Purchaser = "purchaser";
    public const string Master = "master";
    public const string Client = "client";
}
