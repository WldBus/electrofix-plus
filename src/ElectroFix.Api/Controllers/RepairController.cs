using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// Устройства, приёмка, заказ-наряды, гарантия. Пока только чтение:
// операции (открыть наряд, назначить мастера и т. д.) появятся вместе с рабочими местами сотрудников
[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api")]
public class RepairController(RepairService repair) : ControllerBase
{
    [HttpGet("devices")]
    public Task<PagedResult<DeviceDto>> Devices([FromQuery] ListQuery q, int? clientId) =>
        repair.ListDevicesAsync(q, clientId);

    [HttpGet("devices/{id:int}")]
    public Task<DeviceDto> Device(int id) => repair.GetDeviceAsync(id);

    [HttpGet("checkins")]
    public Task<PagedResult<CheckinDto>> Checkins([FromQuery] StatusListQuery q) => repair.ListCheckinsAsync(q);

    [HttpGet("checkins/{id:int}")]
    public Task<CheckinDto> Checkin(int id) => repair.GetCheckinAsync(id);

    [HttpGet("work-orders")]
    public Task<PagedResult<WorkOrderDto>> WorkOrders([FromQuery] WorkOrderListQuery q) => repair.ListWorkOrdersAsync(q);

    [HttpGet("work-orders/{id:int}")]
    public Task<WorkOrderDetailsDto> WorkOrder(int id) => repair.GetWorkOrderAsync(id);

    [HttpGet("warranty-cases")]
    public Task<PagedResult<WarrantyCaseDto>> WarrantyCases([FromQuery] StatusListQuery q) => repair.ListWarrantyAsync(q);
}
