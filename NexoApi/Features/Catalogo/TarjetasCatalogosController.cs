using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Catalogo.Dtos;

namespace NexoApi.Features.Catalogo;

/// <summary>
/// Catalogo Visions: GruposMayores, GruposMenores, Marcas, Presentaciones.
/// Calca de las tablas GRUPOMAYOR, GRUPOMENOR, MARCA, PRESENTACION de Visions.
/// </summary>
[ApiController]
[Route("api/catalogo")]
[Authorize]
public class TarjetasCatalogosController(ICatalogoService service) : ControllerBase
{
    // ── Iva ─────────────────────────────────────────────────────────────

    [HttpGet("ivas")]
    public async Task<ActionResult<IEnumerable<IvaItem>>> ListarIvas()
        => Ok(await service.ListarIvasAsync());

    [HttpPost("ivas")]
    public async Task<IActionResult> CrearIva(CrearIvaRequest request)
    {
        var id = await service.CrearIvaAsync(request);
        return Ok(new { IvaID = id });
    }

    [HttpPut("ivas/{ivaId:int}")]
    public async Task<IActionResult> ActualizarIva(int ivaId, ActualizarIvaRequest request)
    {
        try { await service.ActualizarIvaAsync(ivaId, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("ivas/{ivaId:int}")]
    public async Task<IActionResult> EliminarIva(int ivaId)
    {
        try { await service.EliminarIvaAsync(ivaId); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── GruposMayores ───────────────────────────────────────────────────

    [HttpGet("grupos-mayores")]
    public async Task<ActionResult<IEnumerable<GrupoMayorItem>>> ListarGruposMayores()
        => Ok(await service.ListarGruposMayoresAsync());

    [HttpPost("grupos-mayores")]
    public async Task<IActionResult> CrearGrupoMayor(CrearGrupoMayorRequest request)
    {
        try { await service.CrearGrupoMayorAsync(request); return Ok(); }
        catch (Exception ex) when (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("Violation"))
        { return Conflict(new { error = $"Ya existe un Grupo Mayor con el codigo '{request.Codigo}'." }); }
    }

    [HttpPut("grupos-mayores/{codigo}")]
    public async Task<IActionResult> ActualizarGrupoMayor(string codigo, ActualizarGrupoMayorRequest request)
    {
        try { await service.ActualizarGrupoMayorAsync(codigo, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("grupos-mayores/{codigo}")]
    public async Task<IActionResult> EliminarGrupoMayor(string codigo)
    {
        try { await service.EliminarGrupoMayorAsync(codigo); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex) when (ex.Message.Contains("REFERENCE") || ex.Message.Contains("FK"))
        { return Conflict(new { error = "No se puede eliminar: tiene Grupos Menores asociados." }); }
    }

    // ── GruposMenores ───────────────────────────────────────────────────

    [HttpGet("grupos-menores")]
    public async Task<ActionResult<IEnumerable<GrupoMenorItem>>> ListarGruposMenores(
        [FromQuery] string? grupoMayor)
        => Ok(await service.ListarGruposMenoresAsync(grupoMayor));

    [HttpPost("grupos-menores")]
    public async Task<IActionResult> CrearGrupoMenor(CrearGrupoMenorRequest request)
    {
        try { await service.CrearGrupoMenorAsync(request); return Ok(); }
        catch (Exception ex) when (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("Violation"))
        { return Conflict(new { error = $"Ya existe un Grupo Menor '{request.Codigo}' en el Grupo Mayor '{request.GrupoMayor}'." }); }
        catch (Exception ex) when (ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("FK"))
        { return BadRequest(new { error = $"El Grupo Mayor '{request.GrupoMayor}' no existe." }); }
    }

    [HttpPut("grupos-menores/{codigo}/{grupoMayor}")]
    public async Task<IActionResult> ActualizarGrupoMenor(string codigo, string grupoMayor, ActualizarGrupoMenorRequest request)
    {
        try { await service.ActualizarGrupoMenorAsync(codigo, grupoMayor, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("grupos-menores/{codigo}/{grupoMayor}")]
    public async Task<IActionResult> EliminarGrupoMenor(string codigo, string grupoMayor)
    {
        try { await service.EliminarGrupoMenorAsync(codigo, grupoMayor); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    // ── Marcas ──────────────────────────────────────────────────────────

    [HttpGet("marcas")]
    public async Task<ActionResult<IEnumerable<MarcaItem>>> ListarMarcas()
        => Ok(await service.ListarMarcasAsync());

    [HttpPost("marcas")]
    public async Task<IActionResult> CrearMarca(CrearMarcaRequest request)
    {
        try { await service.CrearMarcaAsync(request); return Ok(); }
        catch (Exception ex) when (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("Violation"))
        { return Conflict(new { error = $"Ya existe una Marca con el codigo '{request.Codigo}'." }); }
    }

    [HttpPut("marcas/{codigo}")]
    public async Task<IActionResult> ActualizarMarca(string codigo, ActualizarMarcaRequest request)
    {
        try { await service.ActualizarMarcaAsync(codigo, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("marcas/{codigo}")]
    public async Task<IActionResult> EliminarMarca(string codigo)
    {
        try { await service.EliminarMarcaAsync(codigo); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex) when (ex.Message.Contains("REFERENCE") || ex.Message.Contains("FK"))
        { return Conflict(new { error = "No se puede eliminar: hay Tarjetas que usan esta Marca." }); }
    }

    // ── Presentaciones ──────────────────────────────────────────────────

    [HttpGet("presentaciones")]
    public async Task<ActionResult<IEnumerable<PresentacionItem>>> ListarPresentaciones()
        => Ok(await service.ListarPresentacionesAsync());

    [HttpPost("presentaciones")]
    public async Task<IActionResult> CrearPresentacion(CrearPresentacionRequest request)
    {
        try { await service.CrearPresentacionAsync(request); return Ok(); }
        catch (Exception ex) when (ex.Message.Contains("PRIMARY KEY") || ex.Message.Contains("Violation"))
        { return Conflict(new { error = $"Ya existe una Presentacion con el codigo '{request.Codigo}'." }); }
    }

    [HttpPut("presentaciones/{codigo}")]
    public async Task<IActionResult> ActualizarPresentacion(string codigo, ActualizarPresentacionRequest request)
    {
        try { await service.ActualizarPresentacionAsync(codigo, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpDelete("presentaciones/{codigo}")]
    public async Task<IActionResult> EliminarPresentacion(string codigo)
    {
        try { await service.EliminarPresentacionAsync(codigo); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
        catch (Exception ex) when (ex.Message.Contains("REFERENCE") || ex.Message.Contains("FK"))
        { return Conflict(new { error = "No se puede eliminar: hay Tarjetas que usan esta Presentacion." }); }
    }
}
