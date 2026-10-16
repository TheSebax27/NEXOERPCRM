using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Logistica.Dtos;

namespace NexoApi.Features.Logistica;

[ApiController]
[Route("api/logistica")]
[Authorize]
public class LogisticaController : ControllerBase
{
    private readonly ILogisticaService _service;

    public LogisticaController(ILogisticaService service)
    {
        _service = service;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("despachos")]
    public async Task<ActionResult<IEnumerable<DespachoItem>>> ListarDespachos(
        [FromQuery] int? centroCostoId, [FromQuery] string? estado)
        => Ok(await _service.ListarDespachosAsync(centroCostoId, estado));

    [HttpGet("despachos/{id:int}/detalle")]
    public async Task<ActionResult<IEnumerable<DespachoDetalleItem>>> ObtenerDetalle(int id)
        => Ok(await _service.ObtenerDetalleAsync(id));

    [HttpPost("despachos")]
    public async Task<ActionResult> CrearDespacho(CrearDespachoRequest request)
    {
        var id = await _service.CrearDespachoAsync(request, UsuarioActualId);
        return CreatedAtAction(nameof(ListarDespachos), new { }, new CrearDespachoResponse(id));
    }

    [HttpPost("despachos/{id:int}/confirmar-entrega")]
    public async Task<ActionResult> ConfirmarEntrega(int id)
    {
        try
        {
            await _service.ConfirmarEntregaAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("despachos/{id:int}/anular")]
    public async Task<ActionResult> AnularDespacho(int id, AnularDespachoRequest request)
    {
        await _service.AnularDespachoAsync(id, request, UsuarioActualId);
        return NoContent();
    }
}
