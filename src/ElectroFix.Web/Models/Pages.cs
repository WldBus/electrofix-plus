using ElectroFix.Shared;

namespace ElectroFix.Web.Models;

// Модели страниц: данные из Api + форма, которую на странице заполняют

public class CabinetHomePage
{
    public required ClientDto Profile { get; init; }
    public List<CheckinDto> Upcoming { get; init; } = [];
    public List<WorkOrderDto> Active { get; init; } = [];
    public List<WorkOrderDto> WaitingDecision { get; init; } = [];
    public List<WorkOrderDto> WithDebt { get; init; } = [];
}

public class DevicesPage
{
    public List<DeviceDto> Devices { get; init; } = [];
    public DeviceSaveRequest NewDevice { get; init; } = new();
}

// запись на приём: клиент записывает себя, приёмщик — выбранного клиента
public class BookPage
{
    public ClientDto? Client { get; init; }
    public List<DeviceDto> Devices { get; init; } = [];
    public CheckinSaveRequest Form { get; init; } = new();
    public DeviceSaveRequest NewDevice { get; init; } = new();
}

public class OpenPage
{
    public required CheckinDto Checkin { get; init; }
    public OpenWorkOrderRequest Form { get; init; } = new();
}

// Карточка заказ-наряда. Mode — кто смотрит: client, receptionist, master
// (от этого зависят кнопки), справочники нужны только сотрудникам
public class OrderPage
{
    public required WorkOrderDetailsDto Order { get; init; }
    public required string Mode { get; init; }

    // мастер может работать только со своими заказ-нарядами
    public bool CanAct { get; init; } = true;
    public List<ServiceDto> Services { get; init; } = [];
    public List<PartDto> Parts { get; init; } = [];
    public List<MasterDto> Masters { get; init; } = [];

    public string Status => Order.Header.Status;
    public bool EstimateEditable => CanAct && Mode != RoleCodes.Client && WorkOrderRules.EditableEstimate.Contains(Status);
    public bool HasDraft => Order.Services.Any(s => s.ApprovalStatus == "draft") || Order.Parts.Any(p => p.ApprovalStatus == "draft");
    public bool HasPending => Order.Services.Any(s => s.ApprovalStatus == "pending") || Order.Parts.Any(p => p.ApprovalStatus == "pending");

    // решение по смете: клиент в кабинете или приёмщик со слов клиента
    public bool CanDecide => CanAct && HasPending && Mode is RoleCodes.Client or RoleCodes.Receptionist;
    public bool CanSetTime => CanAct && Mode == RoleCodes.Master && Status is "assigned" or "in_progress";

    public static decimal Sum(OrderServiceDto s) => Math.Round(s.NormHours * s.Rate * (1 + s.DiscountMarkup / 100), 2);
    public static decimal Sum(OrderPartDto p) => Math.Round(p.Quantity * p.Price, 2);

    // итог считаю по согласованным позициям, как и счёт при закрытии
    public decimal ApprovedTotal =>
        Order.Services.Where(s => s.ApprovalStatus == "approved").Sum(Sum) +
        Order.Parts.Where(p => p.ApprovalStatus == "approved").Sum(Sum);
}

public class ReceiptPage
{
    public List<PartDto> Parts { get; init; } = [];
    public StockReceiptRequest Form { get; init; } = new();
}

public class ToIssuePage
{
    public List<PartToIssueDto> Items { get; init; } = [];

    // партии с остатком по каждой детали, чтобы выбрать, откуда выдавать
    public Dictionary<int, List<StockBalanceDto>> Batches { get; init; } = [];
}

public record BelowMinItem(PartDto Part, decimal Available);
