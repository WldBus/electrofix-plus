using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// личный кабинет клиента: только его карточка, устройства, записи и заказ-наряды
[ApiController]
[Authorize(Roles = Roles.Client)]
[Route("api/my")]
public class MyController(MyService my) : ControllerBase
{
    [HttpGet("profile")]
    public Task<ClientDto> Profile() => my.ProfileAsync();

    [HttpGet("devices")]
    public Task<PagedResult<DeviceDto>> Devices() => my.DevicesAsync();

    [HttpPost("devices")]
    public Task<DeviceDto> AddDevice(DeviceSaveRequest r) => my.AddDeviceAsync(r);

    [HttpGet("checkins")]
    public Task<PagedResult<CheckinDto>> Checkins([FromQuery] StatusListQuery q) => my.CheckinsAsync(q);

    [HttpPost("checkins")]
    public Task<CheckinDto> Book(CheckinSaveRequest r) => my.BookAsync(r);

    [HttpPost("checkins/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        await my.CancelCheckinAsync(id);
        return NoContent();
    }

    [HttpGet("work-orders")]
    public Task<PagedResult<WorkOrderDto>> WorkOrders([FromQuery] WorkOrderListQuery q) => my.WorkOrdersAsync(q);

    [HttpGet("work-orders/{id:int}")]
    public Task<WorkOrderDetailsDto> WorkOrder(int id) => my.WorkOrderAsync(id);

    [HttpPost("work-orders/{id:int}/estimate")]
    public Task<WorkOrderDetailsDto> Decide(int id, EstimateDecisionRequest r) => my.DecideEstimateAsync(id, r);
}
