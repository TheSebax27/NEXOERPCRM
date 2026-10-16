using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Export;
using NexoApi.Features.Catalogo.Dtos;
using NexoApi.Features.Configuracion;

namespace NexoApi.Features.Catalogo;

// Clientes se movio a Features/Crm/CrmController.cs (agosto 2026) -- este
// controller se quedo solo con Centros de Trabajo y Proveedores, el nombre
// de la clase no se cambio para no romper referencias, pero ya no incluye Clientes.
[ApiController]
[Route("api/catalogo")]
[Authorize]
public class ClientesYCentrosTrabajoController : ControllerBase
{
    private readonly ICatalogoService _service;
    private readonly IConfiguracionService _config;

    public ClientesYCentrosTrabajoController(ICatalogoService service, IConfiguracionService config)
    {
        _service = service;
        _config  = config;
    }

    [HttpGet("centros-trabajo")]
    public async Task<ActionResult<IEnumerable<CentroTrabajoItem>>> ListarCentrosTrabajo([FromQuery] bool soloActivos = true)
        => Ok(await _service.ListarCentrosTrabajoAsync(soloActivos));

    [HttpPost("centros-trabajo")]
    [Authorize]
    public async Task<ActionResult> CrearCentroTrabajo(CrearCentroTrabajoRequest request)
    {
        var id = await _service.CrearCentroTrabajoAsync(request);
        return CreatedAtAction(nameof(ListarCentrosTrabajo), new { }, new { CentroTrabajoID = id });
    }

    [HttpPut("centros-trabajo/{id:int}")]
    [Authorize]
    public async Task<ActionResult> ActualizarCentroTrabajo(int id, ActualizarCentroTrabajoRequest request)
    {
        try
        {
            await _service.ActualizarCentroTrabajoAsync(id, request);
            return Ok();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("proveedores")]
    public async Task<ActionResult<IEnumerable<ProveedorItem>>> ListarProveedores()
        => Ok(await _service.ListarProveedoresAsync());

    [HttpGet("proveedores/{id:int}")]
    public async Task<ActionResult<ProveedorItem>> ObtenerProveedor(int id)
    {
        var p = await _service.ObtenerProveedorAsync(id);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost("proveedores")]
    [Authorize]
    public async Task<ActionResult> CrearProveedor(CrearProveedorRequest request)
    {
        var id = await _service.CrearProveedorAsync(request);
        return CreatedAtAction(nameof(ListarProveedores), new { }, new { ProveedorID = id });
    }

    [HttpPut("proveedores/{id:int}")]
    [Authorize]
    public async Task<ActionResult> ActualizarProveedor(int id, ActualizarProveedorRequest request)
    {
        await _service.ActualizarProveedorAsync(id, request);
        return Ok();
    }

    /// <summary>Consulta si un NIT está registrado como cliente y/o proveedor en NEXO.</summary>
    [HttpGet("verificar-dual-rol")]
    public async Task<ActionResult<DualRolInfo>> VerificarDualRol([FromQuery] string nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return BadRequest("NIT requerido.");
        return Ok(await _service.VerificarDualRolAsync(nit.Trim()));
    }

    /// <summary>Crea un proveedor copiando los datos del cliente indicado. Si ya existe como proveedor, retorna el ID existente.</summary>
    [HttpPost("proveedores/desde-cliente/{clienteId:int}")]
    [Authorize]
    public async Task<ActionResult> AgregarComoProveedorDesdeCliente(int clienteId)
    {
        var id = await _service.AgregarComoProveedorDesdeClienteAsync(clienteId);
        return Ok(new { ProveedorID = id });
    }

    /// <summary>Crea un cliente copiando los datos del proveedor indicado. Si ya existe como cliente, retorna el ID existente.</summary>
    [HttpPost("proveedores/{proveedorId:int}/agregar-como-cliente")]
    [Authorize]
    public async Task<ActionResult> AgregarComoClienteDesdeProveedor(int proveedorId)
    {
        var id = await _service.AgregarComoClienteDesdeProveedorAsync(proveedorId);
        return Ok(new { ClienteID = id });
    }

    [HttpGet("proveedores/{id:int}/historial")]
    public async Task<ActionResult> ObtenerHistorialProveedor(int id)
        => Ok(await _service.ObtenerHistorialProveedorAsync(id));

    // ── Exportar proveedores ─────────────────────────────────────────────────

    [HttpGet("proveedores/exportar/excel")]
    public async Task<IActionResult> ExportarProveedoresExcel()
    {
        var proveedores = await _service.ListarProveedoresAsync();
        var bytes       = ExportService.GenerarExcelProveedores(proveedores);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"proveedores_{NexoApi.Common.ColombiaClock.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("proveedores/exportar/pdf")]
    public async Task<IActionResult> ExportarProveedoresPdf()
    {
        var proveedores = await _service.ListarProveedoresAsync();
        var empresa     = await _config.ObtenerEmpresaAsync();
        var bytes       = ExportService.GenerarPdfProveedores(proveedores, empresa.NombreEmpresa);
        return File(bytes, "application/pdf", $"proveedores_{NexoApi.Common.ColombiaClock.Now:yyyyMMdd}.pdf");
    }
}
