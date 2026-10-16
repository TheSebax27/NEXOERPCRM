using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Documentos.Dtos;

namespace NexoApi.Features.Documentos;

[Authorize]
[ApiController]
[Route("api/documentos")]
public class DocumentosController : ControllerBase
{
    private readonly IDocumentosService _service;

    public DocumentosController(IDocumentosService service) => _service = service;

    // ── Plantillas ────────────────────────────────────────────────────────────

    [HttpGet("plantillas")]
    public async Task<ActionResult<IEnumerable<PlantillaResumen>>> ListarPlantillas()
        => Ok(await _service.ListarPlantillasAsync());

    [HttpGet("plantillas/{id:int}")]
    public async Task<ActionResult<PlantillaDetalle>> ObtenerPlantilla(int id)
    {
        var plantilla = await _service.ObtenerPlantillaAsync(id);
        return plantilla is null ? NotFound() : Ok(plantilla);
    }

    [HttpPost("plantillas")]
    public async Task<ActionResult<int>> CrearPlantilla([FromBody] CrearPlantillaRequest request)
    {
        var id = await _service.CrearPlantillaAsync(request, ObtenerUsuarioId());
        return Ok(id);
    }

    [HttpPut("plantillas/{id:int}")]
    public async Task<IActionResult> ActualizarPlantilla(int id, [FromBody] ActualizarPlantillaRequest request)
    {
        var ok = await _service.ActualizarPlantillaAsync(id, request, ObtenerUsuarioId());
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("plantillas/importar")]
    public ActionResult<ImportarPlantillaResult> ImportarPlantilla([FromBody] ImportarArchivoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Base64))
            return BadRequest("Archivo requerido.");
        try
        {
            return Ok(_service.ImportarDesdeArchivo(request));
        }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
        catch (Exception) { return BadRequest("No se pudo leer el archivo. Verifica que no esté protegido o dañado."); }
    }

    [HttpDelete("plantillas/{id:int}")]
    public async Task<IActionResult> EliminarPlantilla(int id)
    {
        var ok = await _service.EliminarPlantillaAsync(id);
        return ok ? NoContent() : BadRequest("No se puede eliminar: la plantilla tiene documentos generados.");
    }

    // ── Documentos generados ──────────────────────────────────────────────────

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DocumentoResumen>>> ListarDocumentos(
        [FromQuery] string? estado,
        [FromQuery] int? clienteId,
        [FromQuery] int? proveedorId,
        [FromQuery] int? empleadoId)
        => Ok(await _service.ListarDocumentosAsync(estado, clienteId, proveedorId, empleadoId));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentoDetalle>> ObtenerDocumento(int id)
    {
        var doc = await _service.ObtenerDocumentoAsync(id);
        return doc is null ? NotFound() : Ok(doc);
    }

    [HttpPost]
    public async Task<ActionResult<int>> GenerarDocumento([FromBody] GenerarDocumentoRequest request)
    {
        var id = await _service.GenerarDocumentoAsync(request, ObtenerUsuarioId());
        return Ok(id);
    }

    private int ObtenerUsuarioId()
    {
        var claim = User.FindFirst("UsuarioID") ?? User.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }

    [HttpPost("{id:int}/firma")]
    public async Task<IActionResult> AgregarFirma(int id, [FromBody] AgregarFirmaRequest request)
    {
        var ok = await _service.AgregarFirmaAsync(id, request);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("{id:int}/archivar")]
    public async Task<IActionResult> ArchivarDocumento(int id)
    {
        var ok = await _service.ArchivarDocumentoAsync(id);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> ExportarPdf(int id)
    {
        try
        {
            var bytes = await _service.ExportarPdfAsync(id);
            return File(bytes, "application/pdf", $"documento-{id}.pdf");
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
