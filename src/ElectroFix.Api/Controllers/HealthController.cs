using ElectroFix.Api.Data;

using Microsoft.AspNetCore.Mvc;

namespace ElectroFix.Api.Controllers;

// по этому адресу Render будит сервер, а я проверяю, что Api живой и база отвечает
[ApiController]
[Route("api/health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var dbOk = await db.Database.CanConnectAsync();
        var body = new { status = dbOk ? "ok" : "degraded", database = dbOk ? "ok" : "unavailable" };
        return dbOk ? Ok(body) : StatusCode(503, body);
    }
}
