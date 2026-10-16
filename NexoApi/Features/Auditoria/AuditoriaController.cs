using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexoApi.Features.Auditoria;

[ApiController]
[Route("api/auditoria")]
[Authorize]
public class AuditoriaController(IAuditoriaService service) : ControllerBase
{
    [HttpGet("log")]
    public async Task<ActionResult> ListarLog(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? accion,
        [FromQuery] string? modulo,
        [FromQuery] string? nombreUsuario,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 50)
    {
        tamano = Math.Clamp(tamano, 10, 200);
        pagina = Math.Max(1, pagina);

        var items = await service.ListarLogAsync(desde, hasta, accion, modulo, nombreUsuario, pagina, tamano);
        var total = await service.ContarLogAsync(desde, hasta, accion, modulo, nombreUsuario);

        return Ok(new { Items = items, Total = total, Pagina = pagina, Tamano = tamano });
    }
}
