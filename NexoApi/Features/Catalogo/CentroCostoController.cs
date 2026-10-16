using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Catalogo.Dtos;

namespace NexoApi.Features.Catalogo;

[ApiController]
[Route("api/catalogo/centros-costo")]
[Authorize]
public class CentrosCostoController : ControllerBase
{
    private readonly ICatalogoService _service;

    public CentrosCostoController(ICatalogoService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CentroCostoItem>>> Listar([FromQuery] bool soloActivos = true)
    {
        return Ok(await _service.ListarCentrosCostoAsync(soloActivos));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> Crear(CrearCentroCostoRequest request)
    {
        var id = await _service.CrearCentroCostoAsync(request);
        return CreatedAtAction(nameof(Listar), new { CentroCostoID = id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<ActionResult> Actualizar(int id, ActualizarCentroCostoRequest request)
    {
        try
        {
            await _service.ActualizarCentroCostoAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}