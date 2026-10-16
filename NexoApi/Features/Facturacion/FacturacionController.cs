using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Facturacion.Dtos;

namespace NexoApi.Features.Facturacion;

[ApiController]
[Route("api/facturacion")]
[Authorize]
public class FacturacionController : ControllerBase
{
    private readonly IFacturacionService _service;
    private readonly FacturaPdfService _pdf;

    public FacturacionController(IFacturacionService service, FacturaPdfService pdf)
    {
        _service = service;
        _pdf = pdf;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ──────────── Tipos de documento ────────────
    [HttpGet("tipos-documento")]
    public ActionResult<IReadOnlyList<string>> ListarTiposDocumento()
        => Ok(Dtos.TiposDocumento.Todos);

    // Devuelve el siguiente numero de documento segun la configuracion de la empresa.
    // Retorna null si el modo es Manual o Visions no esta disponible.
    [HttpGet("siguiente-nrodoc")]
    public async Task<ActionResult<string?>> ObtenerSiguienteNroDoc([FromQuery] string tipDoc = "FACTURA")
        => Ok(await _service.ObtenerSiguienteNroDocAsync(tipDoc));

    // ──────────── Facturas ────────────
    [HttpGet("facturas")]
    public async Task<ActionResult<FacturasPaginadasResponse>> ListarFacturas(
        [FromQuery] int? clienteId,
        [FromQuery] int? centroCostoId,
        [FromQuery] string? tipDoc,
        [FromQuery] string? estado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] string? texto,
        [FromQuery] bool soloNoPagadas = false,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 100)
        => Ok(await _service.ListarFacturasAsync(clienteId, centroCostoId, tipDoc, estado, desde, hasta, texto, soloNoPagadas, pagina, tamano));

    [HttpPost("facturas")]
    public async Task<ActionResult> CrearFactura(CrearFacturaRequest request)
    {
        try
        {
            var id = await _service.CrearFacturaAsync(request, UsuarioActualId);
            return Ok(new { facturaId = id });
        }

        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("facturas/{id:int}")]
    public async Task<ActionResult> EliminarFactura(int id)
    {
        try
        {
            await _service.EliminarFacturaAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpGet("facturas/{id:int}/lineas")]
    public async Task<ActionResult<IEnumerable<FacturaLineaItem>>> ListarLineas(int id)
        => Ok(await _service.ListarLineasAsync(id));

    [HttpGet("facturas/{id:int}/stock-lineas")]
    public async Task<ActionResult<IEnumerable<FacturaLineaStockItem>>> ObtenerStockLineas(int id)
        => Ok(await _service.ObtenerStockLineasAsync(id));

    [HttpGet("stock-fallido-count")]
    public async Task<ActionResult<int>> StockFallidoCount()
        => Ok(await _service.ContarStockFallidoAsync());

    [HttpPost("facturas/{id:int}/descontar-stock")]
    public async Task<ActionResult> DescontarStock(int id)
    {
        try
        {
            await _service.DescontarStockAsync(id, UsuarioActualId);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { error = ex.Message }); }
    }

    [HttpPost("facturas/{id:int}/confirmar-visions")]
    public async Task<ActionResult> ConfirmarVisions(int id)
    {
        try
        {
            await _service.ConfirmarVisionsAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("facturas/{id:int}/verificar-produccion")]
    public async Task<ActionResult<List<VerificarProduccionItem>>> VerificarProduccion(int id)
        => Ok(await _service.VerificarProduccionFacturaAsync(id));

    [HttpPost("facturas/{id:int}/auto-producir")]
    public async Task<ActionResult<List<AutoProducirResultItem>>> AutoProducir(int id)
        => Ok(await _service.AutoProducirFacturaAsync(id, UsuarioActualId));

    // ──────────── Pagos ────────────
    [HttpGet("facturas/{id:int}/pagos")]
    public async Task<ActionResult<IEnumerable<PagoItem>>> ListarPagos(int id)
        => Ok(await _service.ListarPagosAsync(id));

    [HttpPost("pagos")]
    public async Task<ActionResult> CrearPago(CrearPagoRequest request)
    {
        try
        {
            var id = await _service.CrearPagoAsync(request, UsuarioActualId);
            return Ok(new { pagoId = id });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("ultimo-precio")]
    public async Task<ActionResult<UltimoPrecioItem?>> UltimoPrecio(
        [FromQuery] int clienteId, [FromQuery] int articuloId)
        => Ok(await _service.ObtenerUltimoPrecioAsync(clienteId, articuloId));

    [HttpGet("facturas/{id:int}/pdf")]
    public async Task<ActionResult> DescargarPdf(int id)
    {
        try
        {
            var bytes = await _pdf.GenerarAsync(id);
            return File(bytes, "application/pdf", $"factura-{id}.pdf");
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
