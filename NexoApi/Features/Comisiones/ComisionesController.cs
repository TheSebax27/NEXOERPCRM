using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Crm.Dtos;

namespace NexoApi.Features.Comisiones;

[ApiController]
[Route("api/comisiones")]
[Authorize]
public class ComisionesController(IComisionesService svc) : ControllerBase
{
    [HttpGet("config")]
    [Authorize(Roles = "Administracion,RRHH")]
    public async Task<ActionResult<IEnumerable<ConfigComisionItem>>> ListarConfig()
        => Ok(await svc.ListarConfigAsync());

    [HttpPost("config")]
    [Authorize(Roles = "Administracion,RRHH")]
    public async Task<ActionResult> GuardarConfig([FromBody] GuardarConfigComisionRequest req)
    {
        await svc.GuardarConfigAsync(req);
        return Ok();
    }

    [HttpGet("dashboard/{empleadoId:int}")]
    [Authorize(Roles = "Administracion,RRHH,Ventas")]
    public async Task<ActionResult<DashboardComisionItem>> Dashboard(
        int empleadoId,
        [FromQuery] int? mes,
        [FromQuery] int? anio)
    {
        var m = mes  ?? NexoApi.Common.ColombiaClock.Now.Month;
        var a = anio ?? NexoApi.Common.ColombiaClock.Now.Year;
        var result = await svc.ObtenerDashboardAsync(empleadoId, m, a);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("resumen")]
    [Authorize(Roles = "Administracion,RRHH")]
    public async Task<ActionResult<IEnumerable<ResumenComisionesItem>>> Resumen(
        [FromQuery] int? mes,
        [FromQuery] int? anio)
    {
        var m = mes  ?? NexoApi.Common.ColombiaClock.Now.Month;
        var a = anio ?? NexoApi.Common.ColombiaClock.Now.Year;
        return Ok(await svc.ResumenTodosAsync(m, a));
    }
}
