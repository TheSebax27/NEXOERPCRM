using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Finanzas.Dtos;

namespace NexoApi.Features.Finanzas;

[ApiController]
[Route("api/finanzas")]
[Authorize]
public class FinanzasController : ControllerBase
{
    private readonly IFinanzasService _service;
    public FinanzasController(IFinanzasService service) => _service = service;

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("gastos")]
    public async Task<ActionResult<IEnumerable<GastoItem>>> Listar(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] string? categoria, [FromQuery] int? centroCostoId)
        => Ok(await _service.ListarGastosAsync(desde, hasta, categoria, centroCostoId));

    [HttpPost("gastos")]
    public async Task<ActionResult> Crear(CrearGastoRequest request)
    {
        var id = await _service.CrearGastoAsync(request, UsuarioActualId);
        return CreatedAtAction(nameof(Listar), new { }, new { gastoId = id });
    }

    [HttpPut("gastos/{id:int}")]
    public async Task<ActionResult> Actualizar(int id, ActualizarGastoRequest request)
    {
        try { await _service.ActualizarGastoAsync(id, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("gastos/{id:int}")]
    public async Task<ActionResult> Eliminar(int id)
    {
        try { await _service.EliminarGastoAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("gastos/resumen-categoria")]
    public async Task<ActionResult<IEnumerable<ResumenGastoCategoria>>> ResumenCategoria(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        => Ok(await _service.ResumenPorCategoriaAsync(desde, hasta));

    [HttpGet("gastos/resumen-mes")]
    public async Task<ActionResult<IEnumerable<ResumenGastoMes>>> ResumenMes(
        [FromQuery] int meses = 12)
        => Ok(await _service.ResumenPorMesAsync(meses));
}
