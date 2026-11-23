using System.Linq.Expressions;

using ElectroFix.Api.Common;
using ElectroFix.Api.Data;
using ElectroFix.Api.Entities;
using ElectroFix.Shared;

using Microsoft.EntityFrameworkCore;

namespace ElectroFix.Api.Services;

// Устройства, записи на приём, заказ-наряды и гарантия — чтение (операции в WorkflowService).
// Везде IgnoreQueryFilters(): история должна показываться целиком,
// даже если клиент, работа или сотрудник уже лежат в корзине
public class RepairService(AppDbContext db)
{
    private static readonly Expression<Func<Device, DeviceDto>> DeviceToDto = d => new DeviceDto(
        d.DeviceId, d.ClientId, d.Client!.DisplayName, d.Type, d.Brand, d.Model,
        d.ManufactureYear, d.SerialNumber, d.InventoryNumber, d.RuntimeHours);

    private static readonly Expression<Func<Checkin, CheckinDto>> CheckinToDto = c => new CheckinDto(
        c.CheckinId, c.DeviceId, c.Device!.Type + " " + c.Device.Brand + " " + c.Device.Model,
        c.Device.Client!.DisplayName, c.Receptionist != null ? c.Receptionist.FullName : null,
        c.Branch, c.SlotStart, c.SlotEnd, c.ServicesRequested, c.Defects, c.ConditionState,
        c.Priority, c.Status, c.WorkOrder != null ? c.WorkOrder.WorkOrderId : null);

    private static readonly Expression<Func<WorkOrderView, WorkOrderDto>> WorkOrderToDto = w => new WorkOrderDto(
        w.WorkOrderId, w.WorkOrderNumber, w.Status, w.ClientId, w.Client, w.Device, w.SerialNumber, w.Branch,
        w.Receptionist, w.MasterId, w.Master, w.TotalAmount, w.InvoiceNumber, w.InvoiceStatus, w.OpenedAt, w.ClosedAt);

    private static readonly Expression<Func<WarrantyCase, WarrantyCaseDto>> WarrantyToDto = w => new WarrantyCaseDto(
        w.WarrantyCaseId, w.WorkOrderId, w.WorkOrder!.WorkOrderNumber, w.Reason, w.InspectionNotes,
        w.Decision, w.SettlementAmount, w.CreatedAt);

    private static readonly Dictionary<string, Expression<Func<Device, object?>>> DeviceSort = new()
    {
        ["id"] = d => d.DeviceId,
        ["brand"] = d => d.Brand,
        ["type"] = d => d.Type,
        ["client"] = d => d.Client!.DisplayName,
    };

    private static readonly Dictionary<string, Expression<Func<Checkin, object?>>> CheckinSort = new()
    {
        ["id"] = c => c.CheckinId,
        ["slot"] = c => c.SlotStart,
        ["status"] = c => c.Status,
        ["priority"] = c => c.Priority,
    };

    private static readonly Dictionary<string, Expression<Func<WorkOrderView, object?>>> WorkOrderSort = new()
    {
        ["id"] = w => w.WorkOrderId,
        ["number"] = w => w.WorkOrderNumber,
        ["opened"] = w => w.OpenedAt,
        ["status"] = w => w.Status,
        ["client"] = w => w.Client,
        ["total"] = w => w.TotalAmount,
    };

    private static readonly Dictionary<string, Expression<Func<WarrantyCase, object?>>> WarrantySort = new()
    {
        ["id"] = w => w.WarrantyCaseId,
        ["created"] = w => w.CreatedAt,
        ["decision"] = w => w.Decision,
    };

    public Task<PagedResult<DeviceDto>> ListDevicesAsync(ListQuery q, int? clientId)
    {
        var query = db.Devices.IgnoreQueryFilters();
        if (q.Search.Like() is { } like)
        {
            query = query.Where(d => EF.Functions.ILike(d.Brand, like) || EF.Functions.ILike(d.Model, like)
                                     || EF.Functions.ILike(d.SerialNumber, like));
        }
        if (clientId != null)
        {
            query = query.Where(d => d.ClientId == clientId);
        }
        return query.SortBy(q.Sort, DeviceSort, "id").ToPagedAsync(q, DeviceToDto);
    }

    public async Task<DeviceDto> GetDeviceAsync(int id) =>
        await db.Devices.IgnoreQueryFilters().Where(d => d.DeviceId == id).Select(DeviceToDto).SingleOrDefaultAsync()
        ?? throw ApiException.NotFound("Устройство");

    public Task<PagedResult<CheckinDto>> ListCheckinsAsync(StatusListQuery q, int? clientId = null)
    {
        var query = db.Checkins.IgnoreQueryFilters();
        if (clientId != null)
        {
            query = query.Where(c => c.Device!.ClientId == clientId);
        }
        if (q.Search.Like() is { } like)
        {
            query = query.Where(c => EF.Functions.ILike(c.Device!.Client!.DisplayName, like)
                                     || EF.Functions.ILike(c.Device.SerialNumber, like)
                                     || EF.Functions.ILike(c.Branch, like));
        }
        if (!string.IsNullOrEmpty(q.Status))
        {
            query = query.Where(c => c.Status == q.Status);
        }
        return query.SortBy(q.Sort, CheckinSort, "slot,desc").ToPagedAsync(q, CheckinToDto);
    }

    public async Task<CheckinDto> GetCheckinAsync(int id) =>
        await db.Checkins.IgnoreQueryFilters().Where(c => c.CheckinId == id).Select(CheckinToDto).SingleOrDefaultAsync()
        ?? throw ApiException.NotFound("Запись на приём");

    // список заказ-нарядов беру из представления v_work_orders: там уже клиент, устройство и сумма
    public Task<PagedResult<WorkOrderDto>> ListWorkOrdersAsync(WorkOrderListQuery q)
    {
        var query = db.WorkOrdersView.AsQueryable();
        if (q.Search.Like() is { } like)
        {
            query = query.Where(w => EF.Functions.ILike(w.WorkOrderNumber, like) || EF.Functions.ILike(w.Client, like)
                                     || EF.Functions.ILike(w.Device, like));
        }
        if (!string.IsNullOrEmpty(q.Status))
        {
            query = query.Where(w => w.Status == q.Status);
        }
        if (q.MasterId != null)
        {
            query = query.Where(w => w.MasterId == q.MasterId);
        }
        if (q.ClientId != null)
        {
            query = query.Where(w => w.ClientId == q.ClientId);
        }
        return query.SortBy(q.Sort, WorkOrderSort, "opened,desc").ToPagedAsync(q, WorkOrderToDto);
    }

    public async Task<WorkOrderDetailsDto> GetWorkOrderAsync(int id)
    {
        var header = await db.WorkOrdersView.Where(w => w.WorkOrderId == id).Select(WorkOrderToDto).SingleOrDefaultAsync()
                     ?? throw ApiException.NotFound("Заказ-наряд");
        var checkinId = await db.WorkOrders.Where(w => w.WorkOrderId == id).Select(w => w.CheckinId).SingleAsync();

        var services = await db.OrderServices.IgnoreQueryFilters().Where(s => s.WorkOrderId == id)
            .OrderBy(s => s.EstimateVersion).ThenBy(s => s.OrderServiceId)
            .Select(s => new OrderServiceDto(s.OrderServiceId, s.ServiceId, s.Service!.Name, s.Service.NormHours,
                s.Service.Rate, s.EstimateVersion, s.ActualTime, s.ApprovalStatus, s.DiscountMarkup, s.IsEmergency))
            .ToListAsync();

        var parts = await db.OrderParts.IgnoreQueryFilters().Where(p => p.WorkOrderId == id)
            .OrderBy(p => p.OrderPartId)
            .Select(p => new OrderPartDto(p.OrderPartId, p.PartId, p.Part!.Article, p.Part.Name, p.Quantity, p.Price,
                p.Batch, p.ApprovalStatus))
            .ToListAsync();

        var invoices = await db.InvoicesView.Where(i => i.WorkOrderNumber == header.WorkOrderNumber)
            .OrderBy(i => i.IssuedAt).Select(FinanceService.InvoiceToDto).ToListAsync();

        var warranty = await db.WarrantyCases.Where(w => w.WorkOrderId == id)
            .OrderBy(w => w.CreatedAt).Select(WarrantyToDto).ToListAsync();

        return new WorkOrderDetailsDto(header, await GetCheckinAsync(checkinId), services, parts, invoices, warranty);
    }

    public Task<PagedResult<WarrantyCaseDto>> ListWarrantyAsync(StatusListQuery q)
    {
        var query = db.WarrantyCases.AsQueryable();
        if (q.Search.Like() is { } like)
        {
            query = query.Where(w => EF.Functions.ILike(w.Reason, like) || EF.Functions.ILike(w.WorkOrder!.WorkOrderNumber, like));
        }
        // для гарантии «статус» — это решение: pending / free_repair / refusal
        if (!string.IsNullOrEmpty(q.Status))
        {
            query = query.Where(w => w.Decision == q.Status);
        }
        return query.SortBy(q.Sort, WarrantySort, "created,desc").ToPagedAsync(q, WarrantyToDto);
    }
}
