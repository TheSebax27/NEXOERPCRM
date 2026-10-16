using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Crm.Dtos;

namespace NexoApi.Features.Crm;

[ApiController]
[Route("api/crm/automatizacion")]
[Authorize]
public class AutomacionController(IAutomacionService _service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReglaAutomacionItem>>> Listar()
        => Ok(await _service.ListarReglasAsync());

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Actualizar(int id, ActualizarReglaRequest request)
    {
        await _service.ActualizarReglaAsync(id, request);
        return NoContent();
    }

    [HttpPost("{id:int}/ejecutar")]
    public async Task<ActionResult<EjecutarReglaResponse>> EjecutarAhora(int id)
    {
        try
        {
            var creadas = await _service.EjecutarReglaAhoraAsync(id);
            return Ok(new EjecutarReglaResponse(creadas));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("evaluar")]
    public async Task<ActionResult> EvaluarTodas()
    {
        await _service.EvaluarReglasPeriodicasAsync();
        return NoContent();
    }
}
