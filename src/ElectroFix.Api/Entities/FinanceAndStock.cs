namespace ElectroFix.Api.Entities;

public class Invoice
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = "";
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public decimal Amount { get; set; }
    public string? PriceListRef { get; set; }
    public string Status { get; set; } = "unpaid";
    public DateTime IssuedAt { get; set; }
    public List<Payment> Payments { get; set; } = [];
}

public class Payment
{
    public int PaymentId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "";
    public bool IsRefund { get; set; }
    public DateTime PaidAt { get; set; }
}

public class StockMove
{
    public int StockMoveId { get; set; }
    public int PartId { get; set; }
    public Part? Part { get; set; }
    public int? WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public string MoveType { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Batch { get; set; } = "";
    public string? StorageCell { get; set; }
    public int PerformedBy { get; set; }
    public User? Performer { get; set; }
    public DateTime MovedAt { get; set; }
}
