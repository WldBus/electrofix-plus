using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// номенклатура запчастей
// смотрят все сотрудники, номенклатуру ведут закупщик и админ
[ApiController]
[Authorize(Roles = Roles.Staff)]
[Route("api/parts")]
public class PartsController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<PartDto>> List([FromQuery] CatalogListQuery q) => catalog.ListPartsAsync(q);

    [HttpGet("{id:int}")]
    public Task<PartDto> Get(int id) => catalog.GetPartAsync(id);

    [Authorize(Roles = Roles.AdminOrPurchaser)]
    [HttpPost]
    public async Task<IActionResult> Create(PartSaveRequest r)
    {
        var created = await catalog.CreatePartAsync(r);
        return CreatedAtAction(nameof(Get), new { id = created.PartId }, created);
    }

    [Authorize(Roles = Roles.AdminOrPurchaser)]
    [HttpPut("{id:int}")]
    public Task<PartDto> Update(int id, PartSaveRequest r) => catalog.UpdatePartAsync(id, r);

    [Authorize(Roles = Roles.AdminOrPurchaser)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, bool hard = false)
    {
        // в корзину может тот, кому разрешено редактировать, а навсегда — только админ
        if (hard && !User.IsInRole(Roles.Admin))
        {
            return Forbid();
        }
        await (hard ? catalog.DeletePartForeverAsync(id) : catalog.TrashPartAsync(id));
        return NoContent();
    }

    [Authorize(Roles = Roles.AdminOrPurchaser)]
    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        await catalog.RestorePartAsync(id);
        return NoContent();
    }
}
