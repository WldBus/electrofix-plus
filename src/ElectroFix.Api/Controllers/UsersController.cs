using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// учётными записями управляет только админ
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/users")]
public class UsersController(UsersService users) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> List([FromQuery] UserListQuery q) => users.ListAsync(q);

    // набор ролей задан в базе, тут только просмотр со счётчиком пользователей
    [HttpGet("/api/roles")]
    public Task<List<RoleDto>> GetRoles() => users.RolesAsync();

    [HttpGet("{id:int}")]
    public Task<UserDto> Get(int id) => users.GetAsync(id);

    [HttpPost]
    public async Task<IActionResult> Create(UserSaveRequest r)
    {
        var created = await users.CreateAsync(r);
        return CreatedAtAction(nameof(Get), new { id = created.UserId }, created);
    }

    [HttpPut("{id:int}")]
    public Task<UserDto> Update(int id, UserSaveRequest r) => users.UpdateAsync(id, r);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, bool hard = false)
    {
        await (hard ? users.DeleteForeverAsync(id) : users.TrashAsync(id));
        return NoContent();
    }

    [HttpPost("{id:int}/block")]
    public async Task<IActionResult> Block(int id, BlockRequest r)
    {
        await users.BlockAsync(id, r.Reason);
        return NoContent();
    }

    [HttpPost("{id:int}/unblock")]
    public async Task<IActionResult> Unblock(int id)
    {
        await users.UnblockAsync(id);
        return NoContent();
    }

    [HttpPost("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        await users.RestoreAsync(id);
        return NoContent();
    }
}
