using ElectroFix.Api.Common;
using ElectroFix.Api.Services;
using ElectroFix.Shared;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth, UsersService users, CurrentUser current) : ControllerBase
{
    [HttpPost("register")]
    public Task<AuthResponse> Register(RegisterRequest r) => auth.RegisterAsync(r);

    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest r) => auth.LoginAsync(r);

    [HttpPost("refresh")]
    public Task<AuthResponse> Refresh(RefreshRequest r) => auth.RefreshAsync(r);

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest r)
    {
        await auth.LogoutAsync(r, current.Id);
        return NoContent();
    }

    // кто я: удобно сайту и панели после входа
    [Authorize]
    [HttpGet("me")]
    public Task<UserDto> Me() => users.GetAsync(current.Id!.Value);
}
