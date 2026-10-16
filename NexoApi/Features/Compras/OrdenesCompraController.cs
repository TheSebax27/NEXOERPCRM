using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Compras.Dtos;

namespace NexoApi.Features.Compras;

[ApiController]
[Route("api/compras/ordenes")]
[Authorize]
public class OrdenesCompraController : ControllerBase
{
    private readonly IOrdenesCompraService _service;
    private readonly OrdenCompraPdfService _pdf;
    private readonly EntradaVisionsPdfService _entradaPdf;

    public OrdenesCompraController(IOrdenesCompraService service, OrdenCompraPdfService pdf, EntradaVisionsPdfService entradaPdf)
    {
        _service    = service;
        _pdf        = pdf;
        _entradaPdf = entradaPdf;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrdenCompraResumen>>> Listar(
        [FromQuery] string? estado,
        [FromQuery] bool soloActivas = false,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 500)
    {
        var ordenes = await _service.ListarAsync(estado, soloActivas, pagina, tamano);
        return Ok(ordenes);
    }

    [HttpGet("{id:int}/detalle")]
    public async Task<ActionResult<IEnumerable<OrdenCompraDetalleItem>>> ObtenerDetalle(int id)
    {
        var detalle = await _service.ObtenerDetalleAsync(id);
        return Ok(detalle);
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult> Crear(CrearOrdenCompraRequest request)
    {
        var id = await _service.CrearAsync(request, UsuarioActualId);
        return CreatedAtAction(nameof(ObtenerDetalle), new { id }, new { OrdenCompraID = id });
    }

    /// <summary>Recibe (total o parcialmente) una linea especifica de la orden de compra.</summary>
    [HttpPost("detalle/{ordenCompraDetalleId:int}/recibir")]
    [Authorize]
    public async Task<ActionResult> RecibirLinea(int ordenCompraDetalleId, RecibirLineaOrdenCompraRequest request)
    {
        var (loteId, nuevoCostoPromedio) = await _service.RecibirLineaAsync(ordenCompraDetalleId, request, UsuarioActualId);
        return Ok(new { mensaje = "Mercancia recibida.", loteId, nuevoCostoPromedio });
    }

    [HttpGet("comparacion-precios")]
    public async Task<ActionResult<IEnumerable<ComparacionPrecioRow>>> ComparacionPrecios(
        [FromQuery] int? articuloId,
        [FromQuery] int? proveedorId)
        => Ok(await _service.ComparacionPreciosAsync(articuloId, proveedorId));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        try
        {
            await _service.EliminarOrdenCompraAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("entradas-visions")]
    public async Task<ActionResult<EntradasVisionsPaginadasResponse>> ListarEntradasVisions(
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 50)
        => Ok(await _service.ListarEntradasVisionsAsync(pagina, tamano));

    [HttpGet("por-proveedor/{proveedorId:int}")]
    public async Task<ActionResult<IEnumerable<OrdenCompraResumen>>> ListarPorProveedor(int proveedorId)
        => Ok(await _service.ListarPorProveedorAsync(proveedorId));

    [HttpGet("{id:int}/pdf")]
    public async Task<ActionResult> DescargarPdf(int id)
    {
        try
        {
            var bytes = await _pdf.GenerarAsync(id);
            return File(bytes, "application/pdf", $"orden-compra-{id}.pdf");
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("entradas-visions/pdf")]
    public async Task<ActionResult> DescargarPdfEntradaVisions(
        [FromQuery] string tipDoc,
        [FromQuery] string nroDoc,
        [FromQuery] int? centroCostoId)
    {
        try
        {
            var bytes = await _entradaPdf.GenerarAsync(tipDoc, nroDoc, centroCostoId);
            return File(bytes, "application/pdf", $"entrada-visions-{nroDoc}.pdf");
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}