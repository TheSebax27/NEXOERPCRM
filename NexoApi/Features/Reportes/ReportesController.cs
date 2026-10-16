using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Configuracion;
using NexoApi.Features.Reportes.Dtos;

namespace NexoApi.Features.Reportes;

[ApiController]
[Route("api/reportes")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly IReportesService       _svc;
    private readonly IConfiguracionService  _config;

    public ReportesController(IReportesService svc, IConfiguracionService config)
    {
        _svc    = svc;
        _config = config;
    }

    [HttpGet]
    public IActionResult ListarTipos() => Ok(_svc.ListarReportes());

    [HttpPost("ejecutar")]
    public async Task<IActionResult> Ejecutar([FromBody] ReporteRequest req)
    {
        var filas = await _svc.EjecutarAsync(req);
        return Ok(filas);
    }

    [HttpPost("exportar")]
    public async Task<IActionResult> Exportar([FromBody] ReporteRequest req)
    {
        var bytes    = await _svc.ExportarExcelAsync(req);
        var filename = $"reporte_{req.Tipo}_{NexoApi.Common.ColombiaClock.Today:yyyy-MM-dd}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            filename);
    }

    [HttpPost("exportar-pdf")]
    public async Task<IActionResult> ExportarPdf([FromBody] ReporteRequest req)
    {
        var cfg      = await _config.ObtenerEmpresaAsync();
        var bytes    = await _svc.ExportarPdfAsync(req, cfg.NombreEmpresa);
        var filename = $"reporte_{req.Tipo}_{NexoApi.Common.ColombiaClock.Today:yyyy-MM-dd}.pdf";
        return File(bytes, "application/pdf", filename);
    }
}
