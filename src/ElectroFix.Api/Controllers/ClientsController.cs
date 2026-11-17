using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// клиентов ведут приёмщики и админ
[ApiController]
[Authorize(Roles = Roles.AdminOrReceptionist)]
[Route("api/clients")]
public class ClientsController(ClientsService clients) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ClientDto>> List([FromQuery] ClientListQuery q) => clients.ListAsync(q);

    [HttpGet("{id:int}")]
    public Task<ClientDto> Get(int id) => clients.GetAsync(id);

    [HttpPost]
    public async Task<IActionResult> Create(ClientSaveRequest r)
    {
        var created = await clients.CreateAsync(r);
        return CreatedAtAction(nameof(Get), new { id = created.ClientId }, created);
    }

    [HttpPut("{id:int}")]
    public Task<ClientDto> Update(int id, ClientSaveRequest r) => clients.UpdateAsync(id, r);

    // по умолчанию в корзину, ?hard=true — навсегда
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, bool hard = false)
    {
        // в корзину может тот, кому разрешено редактировать, а навсегда — только админ
        if (hard && !User.IsInRole(Roles.Admin))
        {
            return Forbid();
        }
        await (hard ? clients.DeleteForeverAsync(id) : clients.TrashAsync(id));
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        await clients.RestoreAsync(id);
        return NoContent();
    }
}
