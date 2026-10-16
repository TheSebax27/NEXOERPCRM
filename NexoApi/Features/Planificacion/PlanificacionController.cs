using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Planificacion.Dtos;

namespace NexoApi.Features.Planificacion;

[ApiController]
[Route("api/planificacion")]
[Authorize]
public class PlanificacionController : ControllerBase
{
    private readonly IPlanificacionService _service;

    public PlanificacionController(IPlanificacionService service)
    {
        _service = service;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("demanda")]
    public async Task<ActionResult<IEnumerable<DemandaProyectadaItem>>> ListarDemanda([FromQuery] int? centroCostoId)
        => Ok(await _service.ListarDemandaAsync(centroCostoId));

    [HttpPost("demanda")]
    public async Task<ActionResult> CrearDemanda(CrearDemandaRequest request)
    {
        var id = await _service.CrearDemandaAsync(request);
        return CreatedAtAction(nameof(ListarDemanda), new { }, new { demandaId = id });
    }

    [HttpPut("demanda/{id:int}")]
    public async Task<ActionResult> ActualizarDemanda(int id, ActualizarDemandaRequest request)
    {
        try
        {
            await _service.ActualizarDemandaAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- Proyección sugerida (agosto 2026, Planificación v2) ----------

    [HttpGet("demanda/sugerencia")]
    public async Task<ActionResult<SugerenciaDemandaItem>> ObtenerSugerenciaDemanda(
        [FromQuery] int articuloId, [FromQuery] int centroCostoId, [FromQuery] DateTime periodo)
        => Ok(await _service.ObtenerSugerenciaDemandaAsync(articuloId, centroCostoId, periodo));

    [HttpGet("metas-venta")]
    public async Task<ActionResult<IEnumerable<MetaVentaItem>>> ListarMetasVenta([FromQuery] int? centroCostoId)
        => Ok(await _service.ListarMetasVentaAsync(centroCostoId));

    [HttpPost("metas-venta")]
    public async Task<ActionResult> CrearMetaVenta(CrearMetaVentaRequest request)
    {
        var id = await _service.CrearMetaVentaAsync(request);
        return CreatedAtAction(nameof(ListarMetasVenta), new { }, new { metaId = id });
    }

    [HttpPut("metas-venta/{id:int}")]
    public async Task<ActionResult> ActualizarMetaVenta(int id, ActualizarMetaVentaRequest request)
    {
        try
        {
            await _service.ActualizarMetaVentaAsync(id, request, UsuarioActualId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- Versionado de metas (agosto 2026, Planificación v2) ----------

    [HttpGet("metas-venta/{id:int}/historial")]
    public async Task<ActionResult<IEnumerable<MetaVentaHistorialItem>>> ListarHistorialMetaVenta(int id)
        => Ok(await _service.ListarHistorialMetaVentaAsync(id));

    // ---------- Alertas de desviación (agosto 2026, Planificación v2) ----------

    [HttpGet("desviaciones")]
    public async Task<ActionResult<IEnumerable<DesviacionItem>>> ListarDesviaciones([FromQuery] decimal umbral = 70)
        => Ok(await _service.ListarDesviacionesAsync(umbral));

    // ---------- Comparativo histórico multi-mes (agosto 2026, Planificación v2) ----------

    [HttpGet("historico-cumplimiento")]
    public async Task<ActionResult<IEnumerable<HistoricoCumplimientoItem>>> ObtenerHistoricoCumplimiento([FromQuery] int meses = 12)
        => Ok(await _service.ObtenerHistoricoCumplimientoAsync(meses));

    // ---------- Predicción de demanda por media móvil ----------

    [HttpGet("prediccion-demanda")]
    public async Task<ActionResult<IEnumerable<PrediccionDemandaItem>>> PrediccionDemanda()
        => Ok(await _service.PrediccionDemandaAsync());
}
