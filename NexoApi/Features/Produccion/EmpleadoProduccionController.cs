using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Produccion.Dtos;

namespace NexoApi.Features.Produccion;

[ApiController]
[Authorize]
public class EmpleadoProduccionController(IEmpleadoProduccionService svc) : ControllerBase
{
    // ── Empleados por Receta ──────────────────────────────────────

    [HttpGet("api/produccion/recetas/{recetaId:int}/empleados")]
    public async Task<IActionResult> EmpleadosReceta(int recetaId)
        => Ok(await svc.ListarEmpleadosRecetaAsync(recetaId));

    [HttpPut("api/produccion/recetas/{recetaId:int}/empleados")]
    public async Task<IActionResult> GuardarEmpleadosReceta(int recetaId, [FromBody] List<EmpleadoRecetaInput> empleados)
    {
        await svc.GuardarEmpleadosRecetaAsync(recetaId, empleados);
        return NoContent();
    }

    // ── Empleados por Orden ───────────────────────────────────────

    [HttpGet("api/produccion/ordenes/{ordenId:int}/empleados")]
    public async Task<IActionResult> EmpleadosOrden(int ordenId)
        => Ok(await svc.ListarEmpleadosOrdenAsync(ordenId));

    [HttpPut("api/produccion/ordenes/{ordenId:int}/empleados")]
    public async Task<IActionResult> GuardarEmpleadosOrden(int ordenId, [FromBody] List<EmpleadoOrdenInput> empleados)
    {
        await svc.GuardarEmpleadosOrdenAsync(ordenId, empleados);
        return NoContent();
    }
}
