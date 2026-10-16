using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Proyectos.Dtos;

namespace NexoApi.Features.Proyectos;

[ApiController]
[Route("api/proyectos")]
[Authorize]
public class ProyectosController : ControllerBase
{
    private readonly IProyectosService _service;

    public ProyectosController(IProyectosService service)
    {
        _service = service;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProyectoItem>>> ListarProyectos()
        => Ok(await _service.ListarProyectosAsync());

    [HttpPost]
    public async Task<ActionResult> CrearProyecto(CrearProyectoRequest request)
    {
        var id = await _service.CrearProyectoAsync(request);
        return CreatedAtAction(nameof(ListarProyectos), new { }, new { proyectoId = id });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> ActualizarProyecto(int id, ActualizarProyectoRequest request)
    {
        try
        {
            await _service.ActualizarProyectoAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- Tareas ----------

    [HttpGet("{proyectoId:int}/tareas")]
    public async Task<ActionResult<IEnumerable<TareaItem>>> ListarTareas(int proyectoId)
        => Ok(await _service.ListarTareasAsync(proyectoId));

    [HttpPost("tareas")]
    public async Task<ActionResult> CrearTarea(CrearTareaRequest request)
    {
        var id = await _service.CrearTareaAsync(request);
        return Ok(new { tareaId = id });
    }

    [HttpPut("tareas/{id:int}")]
    public async Task<ActionResult> ActualizarTarea(int id, ActualizarTareaRequest request)
    {
        try
        {
            await _service.ActualizarTareaAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    // ---------- Costos ----------

    [HttpGet("{proyectoId:int}/costos")]
    public async Task<ActionResult<IEnumerable<CostoItem>>> ListarCostos(int proyectoId)
        => Ok(await _service.ListarCostosAsync(proyectoId));

    [HttpPost("costos")]
    public async Task<ActionResult> CrearCosto(CrearCostoRequest request)
    {
        var id = await _service.CrearCostoAsync(request, UsuarioActualId);
        return Ok(new { costoId = id });
    }

    // ---------- Hitos / Fases (agosto 2026, Proyectos v2) ----------

    [HttpGet("{proyectoId:int}/hitos")]
    public async Task<ActionResult<IEnumerable<HitoItem>>> ListarHitos(int proyectoId)
        => Ok(await _service.ListarHitosAsync(proyectoId));

    [HttpPost("hitos")]
    public async Task<ActionResult> CrearHito(CrearHitoRequest request)
    {
        var id = await _service.CrearHitoAsync(request);
        return Ok(new { hitoId = id });
    }

    [HttpPut("hitos/{id:int}")]
    public async Task<ActionResult> ActualizarHito(int id, ActualizarHitoRequest request)
    {
        try
        {
            await _service.ActualizarHitoAsync(id, request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- Documentos (agosto 2026, Proyectos v2) ----------

    [HttpGet("{proyectoId:int}/documentos")]
    public async Task<ActionResult<IEnumerable<ProyectoDocumentoItem>>> ListarDocumentos(int proyectoId)
        => Ok(await _service.ListarDocumentosAsync(proyectoId));

    [HttpPost("{proyectoId:int}/documentos")]
    public async Task<ActionResult> SubirDocumento(int proyectoId, SubirDocumentoProyectoRequest request)
    {
        if (Convert.FromBase64String(request.Base64).Length > 5_000_000)
            return BadRequest(new { error = "El documento es muy grande (máximo 5 MB)." });

        var documentoId = await _service.SubirDocumentoAsync(proyectoId, request, UsuarioActualId);
        return Ok(new { documentoId });
    }

    [HttpGet("documentos/{id:int}")]
    public async Task<IActionResult> DescargarDocumento(int id)
    {
        var documento = await _service.ObtenerDocumentoAsync(id);
        if (documento is null)
            return NotFound();

        return File(documento.Value.Datos, documento.Value.ContentType, documento.Value.NombreArchivo);
    }

    [HttpDelete("documentos/{id:int}")]
    public async Task<ActionResult> EliminarDocumento(int id)
    {
        try
        {
            await _service.EliminarDocumentoAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // ---------- Comentarios / Bitacora (agosto 2026, Proyectos v2) ----------

    [HttpGet("{proyectoId:int}/comentarios")]
    public async Task<ActionResult<IEnumerable<ComentarioProyectoItem>>> ListarComentarios(int proyectoId)
        => Ok(await _service.ListarComentariosAsync(proyectoId));

    [HttpPost("comentarios")]
    public async Task<ActionResult> CrearComentario(CrearComentarioRequest request)
    {
        var id = await _service.CrearComentarioAsync(request, UsuarioActualId);
        return Ok(new { comentarioId = id });
    }

    // ---------- Alertas (agosto 2026, Proyectos v2) ----------

    [HttpGet("alertas")]
    public async Task<ActionResult<IEnumerable<ProyectoAlertaItem>>> ListarAlertas()
        => Ok(await _service.ListarAlertasAsync());
}
