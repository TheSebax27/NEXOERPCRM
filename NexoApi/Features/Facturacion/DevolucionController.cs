using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Facturacion.Dtos;

namespace NexoApi.Features.Facturacion;

[ApiController]
[Route("api/facturacion/devoluciones")]
[Authorize]
public class DevolucionController(IDevolucionService service) : ControllerBase
{
    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DevolucionItem>>> Listar(
        [FromQuery] string? tipo,
        [FromQuery] int? clienteId,
        [FromQuery] int? proveedorId,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta)
        => Ok(await service.ListarAsync(tipo, clienteId, proveedorId, desde, hasta));

    [HttpPost]
    public async Task<ActionResult> Crear(CrearDevolucionRequest request)
    {
        var id = await service.CrearAsync(request, UsuarioActualId);
        return CreatedAtAction(nameof(ObtenerLineas), new { id }, new { DevolucionID = id });
    }

    [HttpGet("{id:int}/lineas")]
    public async Task<ActionResult<IEnumerable<DevolucionLineaItem>>> ObtenerLineas(int id)
        => Ok(await service.ListarLineasAsync(id));

    [HttpGet("analytics")]
    public async Task<ActionResult<DevolucionesAnalytics>> Analytics([FromQuery] int meses = 6)
        => Ok(await service.ObtenerAnalyticsAsync(meses));
}
