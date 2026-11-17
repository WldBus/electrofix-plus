namespace ElectroFix.Api.Entities;

// запись на приём и приёмка устройства
public class Checkin
{
    public int CheckinId { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }
    public int? ReceptionistId { get; set; }
    public User? Receptionist { get; set; }
    public string Branch { get; set; } = "";
    public DateTime SlotStart { get; set; }
    public DateTime SlotEnd { get; set; }
    public string? ServicesRequested { get; set; }
    public string? Notes { get; set; }
    public string? Photos { get; set; }
    public string? Defects { get; set; }
    public string? ConditionState { get; set; }
    public int? RuntimeAtCheckin { get; set; }
    public string Priority { get; set; } = "normal";
    public string Status { get; set; } = "booked";
    public WorkOrder? WorkOrder { get; set; }
}

public class WorkOrder
{
    public int WorkOrderId { get; set; }
    public string WorkOrderNumber { get; set; } = "";
    public int CheckinId { get; set; }
    public Checkin? Checkin { get; set; }
    public int ReceptionistId { get; set; }
    public User? Receptionist { get; set; }
    public int? MasterId { get; set; }
    public User? Master { get; set; }
    public string Status { get; set; } = "new";
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public List<OrderService> Services { get; set; } = [];
    public List<OrderPart> Parts { get; set; } = [];
}

// позиция сметы: работа (связь M:N заказ-наряд – справочник работ)
public class OrderService
{
    public int OrderServiceId { get; set; }
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public int ServiceId { get; set; }
    public Service? Service { get; set; }
    public int EstimateVersion { get; set; } = 1;
    public decimal ActualTime { get; set; }
    public string ApprovalStatus { get; set; } = "draft";
    public decimal DiscountMarkup { get; set; }
    public bool IsEmergency { get; set; }
}

// позиция сметы: запчасть (связь M:N заказ-наряд – номенклатура)
public class OrderPart
{
    public int OrderPartId { get; set; }
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public int PartId { get; set; }
    public Part? Part { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public string? Batch { get; set; }
    public string ApprovalStatus { get; set; } = "draft";
}

public class WarrantyCase
{
    public int WarrantyCaseId { get; set; }
    public int WorkOrderId { get; set; }
    public WorkOrder? WorkOrder { get; set; }
    public string Reason { get; set; } = "";
    public string? InspectionNotes { get; set; }
    public string Decision { get; set; } = "pending";
    public decimal SettlementAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}
