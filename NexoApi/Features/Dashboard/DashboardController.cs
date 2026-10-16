using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Dashboard.Dtos;

namespace NexoApi.Features.Dashboard;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;
    private readonly IDashboardExportService _exportService;

    public DashboardController(IDashboardService service, IDashboardExportService exportService)
    {
        _service = service;
        _exportService = exportService;
    }

    [HttpGet("plan-vs-real")]
    public async Task<ActionResult<IEnumerable<PlanVsRealPunto>>> PlanVsReal(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var hastaFinal = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var desdeFinal = desde ?? hastaFinal.AddDays(-14);
        return Ok(await _service.ObtenerPlanVsRealAsync(desdeFinal, hastaFinal));
    }

    [HttpGet("distribucion-centro-costo")]
    public async Task<ActionResult<IEnumerable<DistribucionCentroCostoItem>>> DistribucionCentroCosto()
        => Ok(await _service.ObtenerDistribucionCentroCostoAsync());

    [HttpGet("perdidas-por-motivo")]
    public async Task<ActionResult<IEnumerable<PerdidaPorMotivoItem>>> PerdidasPorMotivo(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var hastaFinal = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var desdeFinal = desde ?? hastaFinal.AddDays(-30);
        return Ok(await _service.ObtenerPerdidasPorMotivoAsync(desdeFinal, hastaFinal));
    }

    [HttpGet("tendencia-costo")]
    public async Task<ActionResult<IEnumerable<TendenciaCostoPunto>>> TendenciaCosto([FromQuery] int articuloId)
        => Ok(await _service.ObtenerTendenciaCostoAsync(articuloId));

    [HttpGet("cumplimiento-planificacion")]
    public async Task<ActionResult<IEnumerable<CumplimientoCentroCostoItem>>> CumplimientoPlanificacion()
        => Ok(await _service.ObtenerCumplimientoAsync());

    // Estos 4 resumenes leen de modulos que en su pantalla propia son
    // Admin-only (CRM, RRHH) -- se restringen igual aqui, aunque el resto del
    // Dashboard/BI sea visible para Jefes/Empleados tambien.
    [HttpGet("resumen-crm")]
    [Authorize]
    public async Task<ActionResult<ResumenCrmItem>> ResumenCrm([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var hastaFinal = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var desdeFinal = desde ?? hastaFinal.AddDays(-30);
        return Ok(await _service.ObtenerResumenCrmAsync(desdeFinal, hastaFinal));
    }

    [HttpGet("empleados-por-centro-costo")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<EmpleadosPorCentroCostoItem>>> EmpleadosPorCentroCosto()
        => Ok(await _service.ObtenerEmpleadosPorCentroCostoAsync());

    [HttpGet("resumen-planificacion")]
    public async Task<ActionResult<ResumenPlanificacionItem>> ResumenPlanificacion()
        => Ok(await _service.ObtenerResumenPlanificacionAsync());

    [HttpGet("resumen-inventario")]
    public async Task<ActionResult<ResumenInventarioItem>> ResumenInventario()
        => Ok(await _service.ObtenerResumenInventarioAsync());

    [HttpGet("resumen-facturacion")]
    public async Task<ActionResult<ResumenFacturacionItem>> ResumenFacturacion(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var hastaFinal = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var desdeFinal = desde ?? hastaFinal.AddDays(-30);
        return Ok(await _service.ObtenerResumenFacturacionAsync(desdeFinal, hastaFinal));
    }

    [HttpGet("ingresos-por-mes")]
    public async Task<ActionResult<IEnumerable<IngresoPorMesPunto>>> IngresosPorMes([FromQuery] int meses = 12)
        => Ok(await _service.ObtenerIngresosPorMesAsync(meses));

    [HttpGet("top-clientes")]
    public async Task<ActionResult<IEnumerable<TopClienteItem>>> TopClientes(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int top = 10)
    {
        var hastaFinal = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var desdeFinal = desde ?? hastaFinal.AddMonths(-3);
        return Ok(await _service.ObtenerTopClientesAsync(desdeFinal, hastaFinal, top));
    }

    [HttpGet("top-articulos")]
    public async Task<ActionResult<IEnumerable<TopArticuloItem>>> TopArticulos(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int top = 10)
    {
        var hastaFinal = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var desdeFinal = desde ?? hastaFinal.AddMonths(-3);
        return Ok(await _service.ObtenerTopArticulosAsync(desdeFinal, hastaFinal, top));
    }

    [HttpGet("margen-articulos")]
    public async Task<ActionResult<IEnumerable<MargenArticuloItem>>> MargenArticulos(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta, [FromQuery] int top = 20)
    {
        var hastaFinal = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var desdeFinal = desde ?? hastaFinal.AddMonths(-3);
        return Ok(await _service.ObtenerMargenPorArticuloAsync(desdeFinal, hastaFinal, top));
    }

    [HttpGet("alertas-stock")]
    public async Task<ActionResult<IEnumerable<AlertaStockItem>>> AlertasStock()
        => Ok(await _service.ObtenerAlertasStockAsync());

    [HttpGet("comparativa-yoy")]
    public async Task<ActionResult<IEnumerable<ComparativaMesItem>>> ComparativaYoY([FromQuery] int meses = 12)
        => Ok(await _service.ObtenerComparativaYoYAsync(meses));

    [HttpGet("actividad")]
    public async Task<ActionResult<IEnumerable<ActividadItem>>> ActividadReciente([FromQuery] int n = 20)
        => Ok(await _service.ObtenerActividadRecienteAsync(n));

    [HttpGet("sparklines")]
    public async Task<ActionResult<SparklinesDashboard>> Sparklines()
        => Ok(await _service.ObtenerSparklinesAsync());

    [HttpGet("horarios-pico")]
    public async Task<ActionResult<IEnumerable<HorarioPicoItem>>> HorariosPico([FromQuery] int meses = 3)
        => Ok(await _service.ObtenerHorariosPicoAsync(meses));

    [HttpGet("pares-complementarios")]
    public async Task<ActionResult<IEnumerable<ParComplementarioItem>>> ParesComplementarios([FromQuery] int top = 10)
        => Ok(await _service.ObtenerParesComplementariosAsync(top));

    [HttpGet("exportar/excel")]
    public async Task<IActionResult> ExportarExcel([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var hastaFinal = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var desdeFinal = desde ?? hastaFinal.AddDays(-30);
        var archivo = await _exportService.GenerarExcelAsync(desdeFinal, hastaFinal);

        return File(archivo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"NEXO-Reporte-{NexoApi.Common.ColombiaClock.Now:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("exportar/pdf")]
    public async Task<IActionResult> ExportarPdf([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var hastaFinal = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var desdeFinal = desde ?? hastaFinal.AddDays(-30);
        var archivo = await _exportService.GenerarPdfAsync(desdeFinal, hastaFinal);

        return File(archivo, "application/pdf", $"NEXO-Reporte-{NexoApi.Common.ColombiaClock.Now:yyyyMMdd-HHmm}.pdf");
    }

    [HttpGet("ventas-por-departamento")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<VentasPorDepartamentoItem>>> VentasPorDepartamento()
        => Ok(await _service.ObtenerVentasPorDepartamentoAsync());

    [HttpGet("resumen-maquinaria")]
    public async Task<ActionResult<ResumenMaquinariaItem>> ResumenMaquinaria()
        => Ok(await _service.ObtenerResumenMaquinariaAsync());
}