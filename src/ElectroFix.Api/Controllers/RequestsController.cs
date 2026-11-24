using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ElectroFix.Api.Controllers;

// Открытая часть для сайта: оставить заявку и узнать статус ремонта без входа
[ApiController]
[AllowAnonymous]
[Route("api/public")]
[EnableRateLimiting("public")]
public class PublicController(RequestsService requests) : ControllerBase
{
    [HttpPost("requests")]
    public async Task<object> Create(RepairRequestSaveRequest r) => new { requestId = await requests.CreateAsync(r) };

    [HttpGet("status")]
    public Task<RepairStatusDto> Status([FromQuery] RepairStatusQuery q) => requests.StatusAsync(q);
}

// Заявки видят приёмщик (обрабатывает) и админ (только смотрит)
[ApiController]
[Route("api/requests")]
public class RequestsController(RequestsService requests) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.AdminOrReceptionist)]
    public Task<PagedResult<RepairRequestDto>> List([FromQuery] StatusListQuery q) => requests.ListAsync(q);

    [HttpGet("{id:int}")]
    [Authorize(Roles = Roles.AdminOrReceptionist)]
    public Task<RepairRequestDto> Get(int id) => requests.GetAsync(id);

    [HttpPost("{id:int}/status")]
    [Authorize(Roles = Roles.Receptionist)]
    public Task<RepairRequestDto> SetStatus(int id, RequestStatusChange r) => requests.SetStatusAsync(id, r);
}
