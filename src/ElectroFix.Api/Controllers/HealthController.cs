using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// по этому адресу Render будит сервер, а я проверяю, что Api живой
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
