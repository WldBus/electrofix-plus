namespace ElectroFix.Shared;

// счёт (из представления v_invoices: сумма, оплачено, долг)
public record InvoiceDto(
    int InvoiceId,
    string InvoiceNumber,
    string WorkOrderNumber,
    [property: PersonalName] string Client,
    decimal Amount,
    decimal Paid,
    decimal Debt,
    string Status,
    DateTime IssuedAt);

public record PaymentDto(
    int PaymentId,
    int InvoiceId,
    string InvoiceNumber,
    decimal Amount,
    string Method,
    bool IsRefund,
    DateTime PaidAt);

public record StockMoveDto(
    int StockMoveId,
    int PartId,
    string Article,
    string PartName,
    int? WorkOrderId,
    string? WorkOrderNumber,
    string MoveType,
    decimal Quantity,
    string Batch,
    string? StorageCell,
    [property: PersonalName] string PerformedBy,
    DateTime MovedAt);

// остаток по детали и партии (представление v_stock_balance)
public record StockBalanceDto(
    int PartId,
    string Article,
    string Name,
    string Batch,
    string? StorageCell,
    decimal OnHand,
    decimal Reserved,
    decimal Available);

public record AuditEntryDto(
    int AuditId,
    int? UserId,
    [property: PersonalName] string? UserName,
    string? UserEmail,
    string Action,
    string ObjectTable,
    int? ObjectId,
    [property: PersonalJson] string? BeforeData,
    [property: PersonalJson] string? AfterData,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt);
