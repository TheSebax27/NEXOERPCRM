using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Rrhh.Dtos;

namespace NexoApi.Features.Rrhh;

// Departamentos, Cargos y Ausencias -- catalogos/soporte de RRHH v2 (agosto
// 2026) que no giran alrededor de un solo EmpleadoID como EmpleadosController.
[ApiController]
[Route("api/rrhh")]
[Authorize]
public class RrhhCatalogosController : ControllerBase
{
    private readonly IRrhhService _service;

    public RrhhCatalogosController(IRrhhService service)
    {
        _service = service;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ---------- Departamentos ----------

    [HttpGet("departamentos")]
    public async Task<ActionResult<IEnumerable<DepartamentoItem>>> ListarDepartamentos()
        => Ok(await _service.ListarDepartamentosAsync());

    [HttpPost("departamentos")]
    [Authorize]
    public async Task<ActionResult> CrearDepartamento(CrearDepartamentoRequest request)
    {
        var id = await _service.CrearDepartamentoAsync(request);
        return Ok(new { departamentoId = id });
    }

    [HttpPut("departamentos/{id:int}")]
    [Authorize]
    public async Task<ActionResult> ActualizarDepartamento(int id, ActualizarDepartamentoRequest request)
    {
        try
        {
            await _service.ActualizarDepartamentoAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- Cargos ----------

    [HttpGet("cargos")]
    public async Task<ActionResult<IEnumerable<CargoItem>>> ListarCargos()
        => Ok(await _service.ListarCargosAsync());

    [HttpPost("cargos")]
    [Authorize]
    public async Task<ActionResult> CrearCargo(CrearCargoRequest request)
    {
        var id = await _service.CrearCargoAsync(request);
        return Ok(new { cargoId = id });
    }

    [HttpPut("cargos/{id:int}")]
    [Authorize]
    public async Task<ActionResult> ActualizarCargo(int id, ActualizarCargoRequest request)
    {
        try
        {
            await _service.ActualizarCargoAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- Ausencias y Vacaciones ----------

    [HttpGet("ausencias")]
    public async Task<ActionResult<IEnumerable<AusenciaItem>>> ListarAusencias([FromQuery] int? empleadoId, [FromQuery] string? estado)
        => Ok(await _service.ListarAusenciasAsync(empleadoId, estado));

    [HttpPost("ausencias")]
    public async Task<ActionResult> CrearAusencia(CrearAusenciaRequest request)
    {
        try
        {
            var id = await _service.CrearAusenciaAsync(request, UsuarioActualId);
            return Ok(new { ausenciaId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("ausencias/{id:int}/estado")]
    public async Task<ActionResult> ActualizarEstadoAusencia(int id, ActualizarEstadoAusenciaRequest request)
    {
        try
        {
            await _service.ActualizarEstadoAusenciaAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
