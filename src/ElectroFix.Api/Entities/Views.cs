namespace ElectroFix.Api.Entities;

// Представления из 02_logic.sql. Только чтение, ключа нет,
// в миграции они не попадают (EF знает, что это view)

public class WorkOrderView
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = "";
    public string Status { get; set; } = "";
    public int ClientId { get; set; }
    public string Client { get; set; } = "";
    public string Device { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Branch { get; set; } = "";
    public string Receptionist { get; set; } = "";
    public int? MasterId { get; set; }
    public string? Master { get; set; }
    public decimal TotalAmount { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceStatus { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public class InvoiceView
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public string WorkOrderNumber { get; set; } = "";
    public string Client { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Paid { get; set; }
    public decimal Debt { get; set; }
    public string Status { get; set; } = "";
    public DateTime IssuedAt { get; set; }
}

public class StockBalanceView
{
    public int PartId { get; set; }
    public string Article { get; set; } = "";
    public string Name { get; set; } = "";
    public string Batch { get; set; } = "";
    public string? StorageCell { get; set; }
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available { get; set; }
}
