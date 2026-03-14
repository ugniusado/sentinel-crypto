using Microsoft.AspNetCore.Mvc;
using SentinelCrypto.Server.Models;
using SentinelCrypto.Server.Services;

namespace SentinelCrypto.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ForecastController(MlForecastService svc) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ForecastRequest req) =>
        Ok(await svc.ForecastAsync(req));
}
