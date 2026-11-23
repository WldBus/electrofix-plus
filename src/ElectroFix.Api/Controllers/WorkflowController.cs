using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// Рабочие операции сотрудников. Админ БД сюда не допущен: по заданию он
// операции только просматривает, а делают их приёмщик, мастер и закупщик
[ApiController]
[Route("api")]
public class WorkflowController(WorkflowService workflow) : ControllerBase
{
    // ---------- приёмщик: клиенты приходят и записываются ----------

    [HttpPost("devices")]
    [Authorize(Roles = Roles.Receptionist)]
    public Task<DeviceDto> AddDevice(DeviceSaveRequest r) => workflow.AddDeviceAsync(r.ClientId, r);

    [HttpPost("checkins")]
    [Authorize(Roles = Roles.Receptionist)]
    public Task<CheckinDto> Book(CheckinSaveRequest r) => workflow.BookAsync(r, byClient: false);

    [HttpPost("checkins/{id:int}/status")]
    [Authorize(Roles = Roles.Receptionist)]
    public async Task<IActionResult> CheckinStatus(int id, StatusRequest r)
    {
        await workflow.SetCheckinStatusAsync(id, r.Status);
        return NoContent();
    }

    // приёмка устройства → новый заказ-наряд, в ответе его id
    [HttpPost("checkins/{id:int}/open")]
    [Authorize(Roles = Roles.Receptionist)]
    public async Task<object> Open(int id, OpenWorkOrderRequest r) => new { workOrderId = await workflow.OpenWorkOrderAsync(id, r) };

    // ---------- заказ-наряд: приёмщик и мастер ----------

    [HttpPost("work-orders/{id:int}/status")]
    [Authorize(Roles = Roles.ReceptionistOrMaster)]
    public Task<WorkOrderDetailsDto> Status(int id, StatusRequest r) => workflow.ChangeStatusAsync(id, r.Status);

    [HttpPost("work-orders/{id:int}/assign")]
    [Authorize(Roles = Roles.Receptionist)]
    public Task<WorkOrderDetailsDto> Assign(int id, AssignMasterRequest r) => workflow.AssignMasterAsync(id, r.MasterId);

    [HttpPost("work-orders/{id:int}/services")]
    [Authorize(Roles = Roles.ReceptionistOrMaster)]
    public Task<WorkOrderDetailsDto> AddService(int id, OrderServiceAddRequest r) => workflow.AddServiceAsync(id, r);

    [HttpDelete("work-orders/{id:int}/services/{lineId:int}")]
    [Authorize(Roles = Roles.ReceptionistOrMaster)]
    public Task<WorkOrderDetailsDto> RemoveService(int id, int lineId) => workflow.RemoveLineAsync(id, lineId, isPart: false);

    [HttpPut("work-orders/{id:int}/services/{lineId:int}/time")]
    [Authorize(Roles = Roles.Master)]
    public Task<WorkOrderDetailsDto> ActualTime(int id, int lineId, ActualTimeRequest r) =>
        workflow.SetActualTimeAsync(id, lineId, r.ActualTime);

    [HttpPost("work-orders/{id:int}/parts")]
    [Authorize(Roles = Roles.ReceptionistOrMaster)]
    public Task<WorkOrderDetailsDto> AddPart(int id, OrderPartAddRequest r) => workflow.AddPartAsync(id, r);

    [HttpDelete("work-orders/{id:int}/parts/{lineId:int}")]
    [Authorize(Roles = Roles.ReceptionistOrMaster)]
    public Task<WorkOrderDetailsDto> RemovePart(int id, int lineId) => workflow.RemoveLineAsync(id, lineId, isPart: true);

    [HttpPost("work-orders/{id:int}/send-estimate")]
    [Authorize(Roles = Roles.ReceptionistOrMaster)]
    public Task<WorkOrderDetailsDto> SendEstimate(int id) => workflow.SendEstimateAsync(id);

    // приёмщик отмечает решение клиента, если тот согласовал смету по телефону
    [HttpPost("work-orders/{id:int}/estimate")]
    [Authorize(Roles = Roles.Receptionist)]
    public Task<WorkOrderDetailsDto> Decide(int id, EstimateDecisionRequest r) => workflow.DecideEstimateAsync(id, r);

    [HttpPost("work-orders/{id:int}/close")]
    [Authorize(Roles = Roles.Receptionist)]
    public Task<WorkOrderDetailsDto> Close(int id) => workflow.CloseAsync(id);

    [HttpPost("payments")]
    [Authorize(Roles = Roles.Receptionist)]
    public async Task<IActionResult> Pay(PaymentSaveRequest r)
    {
        await workflow.RegisterPaymentAsync(r);
        return NoContent();
    }

    [HttpGet("masters")]
    [Authorize(Roles = Roles.Staff)]
    public Task<List<MasterDto>> Masters() => workflow.MastersAsync();

    // ---------- закупщик: склад ----------

    [HttpPost("stock/receipt")]
    [Authorize(Roles = Roles.Purchaser)]
    public async Task<IActionResult> Receive(StockReceiptRequest r)
    {
        await workflow.ReceiveAsync(r);
        return NoContent();
    }

    [HttpPost("stock/issue")]
    [Authorize(Roles = Roles.Purchaser)]
    public async Task<IActionResult> Issue(StockIssueRequest r)
    {
        await workflow.IssueAsync(r);
        return NoContent();
    }

    [HttpGet("stock/to-issue")]
    [Authorize(Roles = Roles.Staff)]
    public Task<List<PartToIssueDto>> ToIssue() => workflow.PartsToIssueAsync();

    [HttpGet("stock/batches")]
    [Authorize(Roles = Roles.Staff)]
    public Task<List<StockBalanceDto>> Batches(int partId) => workflow.BatchesAsync(partId);
}

// открытый прайс работ для главной страницы сайта, без входа
[ApiController]
[AllowAnonymous]
[Route("api/catalog")]
public class PublicCatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet("services")]
    public Task<PagedResult<ServiceDto>> Services([FromQuery] CatalogListQuery q)
    {
        // корзину наружу не показываю
        q.Deleted = false;
        return catalog.ListServicesAsync(q);
    }

    [HttpGet("branches")]
    public string[] GetBranches() => Branches.All;
}
