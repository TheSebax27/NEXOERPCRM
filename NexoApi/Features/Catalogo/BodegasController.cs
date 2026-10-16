using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Catalogo.Dtos;

namespace NexoApi.Features.Catalogo;

[ApiController]
[Route("api/catalogo/bodegas")]
[Authorize]
public class BodegasController : ControllerBase
{
    private readonly ICatalogoService _service;

    public BodegasController(ICatalogoService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BodegaItem>>> Listar([FromQuery] int? centroCostoId)
    {
        return Ok(await _service.ListarBodegasAsync(centroCostoId));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> Crear(CrearBodegaRequest request)
    {
        var id = await _service.CrearBodegaAsync(request);
        return CreatedAtAction(nameof(Listar), new { BodegaID = id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<ActionResult> Actualizar(int id, ActualizarBodegaRequest request)
    {
        try
        {
            await _service.ActualizarBodegaAsync(id, request);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<ActionResult> Desactivar(int id)
    {
        try
        {
            await _service.DesactivarBodegaAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}