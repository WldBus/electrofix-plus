namespace ElectroFix.Shared;

public record CheckinDto(
    int CheckinId,
    int DeviceId,
    string Device,
    [property: PersonalName] string ClientName,
    [property: PersonalName] string? Receptionist,
    string Branch,
    DateTime SlotStart,
    DateTime SlotEnd,
    string? ServicesRequested,
    string? Defects,
    string? ConditionState,
    string Priority,
    string Status,
    int? WorkOrderId);

// строка списка заказ-нарядов (из представления v_work_orders)
public record WorkOrderDto(
    int WorkOrderId,
    string WorkOrderNumber,
    string Status,
    int ClientId,
    [property: PersonalName] string Client,
    string Device,
    string SerialNumber,
    string Branch,
    [property: PersonalName] string Receptionist,
    int? MasterId,
    [property: PersonalName] string? Master,
    decimal TotalAmount,
    string? InvoiceNumber,
    string? InvoiceStatus,
    DateTime OpenedAt,
    DateTime? ClosedAt);

public record OrderServiceDto(
    int OrderServiceId,
    int ServiceId,
    string ServiceName,
    decimal NormHours,
    decimal Rate,
    int EstimateVersion,
    decimal ActualTime,
    string ApprovalStatus,
    decimal DiscountMarkup,
    bool IsEmergency);

public record OrderPartDto(
    int OrderPartId,
    int PartId,
    string Article,
    string PartName,
    decimal Quantity,
    decimal Price,
    string? Batch,
    string ApprovalStatus);

// карточка заказ-наряда: шапка + смета + счета + гарантия
public record WorkOrderDetailsDto(
    WorkOrderDto Header,
    CheckinDto Checkin,
    IReadOnlyList<OrderServiceDto> Services,
    IReadOnlyList<OrderPartDto> Parts,
    IReadOnlyList<InvoiceDto> Invoices,
    IReadOnlyList<WarrantyCaseDto> WarrantyCases);

public record WarrantyCaseDto(
    int WarrantyCaseId,
    int WorkOrderId,
    string WorkOrderNumber,
    string Reason,
    string? InspectionNotes,
    string Decision,
    decimal SettlementAmount,
    DateTime CreatedAt);
