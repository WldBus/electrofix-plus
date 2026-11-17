using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// справочник работ
// смотрят все сотрудники, меняет только админ
[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/services")]
public class ServicesController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<ServiceDto>> List([FromQuery] CatalogListQuery q) => catalog.ListServicesAsync(q);

    [HttpGet("{id:int}")]
    public Task<ServiceDto> Get(int id) => catalog.GetServiceAsync(id);

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<IActionResult> Create(ServiceSaveRequest r)
    {
        var created = await catalog.CreateServiceAsync(r);
        return CreatedAtAction(nameof(Get), new { id = created.ServiceId }, created);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    public Task<ServiceDto> Update(int id, ServiceSaveRequest r) => catalog.UpdateServiceAsync(id, r);

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, bool hard = false)
    {
        await (hard ? catalog.DeleteServiceForeverAsync(id) : catalog.TrashServiceAsync(id));
        return NoContent();
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        await catalog.RestoreServiceAsync(id);
        return NoContent();
    }
}
