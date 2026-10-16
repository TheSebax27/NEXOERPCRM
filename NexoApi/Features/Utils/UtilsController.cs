using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common;

namespace NexoApi.Features.Utils;

[ApiController]
[Route("api/utils")]
public class UtilsController : ControllerBase
{
    // Endpoint público — no requiere auth; ideal para embeber imágenes o compartir links
    [AllowAnonymous]
    [HttpGet("qr-png")]
    public IActionResult QrPng([FromQuery] string contenido = "", [FromQuery] int px = 10)
    {
        if (string.IsNullOrWhiteSpace(contenido))
            return BadRequest("contenido requerido");
        px = Math.Clamp(px, 4, 20);
        return File(QrHelper.GenerarPng(contenido, px), "image/png");
    }
}
