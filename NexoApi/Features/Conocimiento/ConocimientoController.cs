using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Conocimiento.Dtos;
using System.Security.Claims;

namespace NexoApi.Features.Conocimiento;

[ApiController]
[Route("api/conocimiento")]
[Authorize]
public class ConocimientoController : ControllerBase
{
    private readonly IConocimientoService _svc;
    private int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public ConocimientoController(IConocimientoService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? categoria, [FromQuery] string? q)
        => Ok(await _svc.ListarAsync(categoria, q));

    [HttpGet("categorias")]
    public async Task<IActionResult> Categorias()
        => Ok(await _svc.ListarCategoriasAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var art = await _svc.ObtenerAsync(id);
        if (art is null) return NotFound();
        await _svc.RegistrarVistaAsync(id);
        return Ok(art);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearArticuloRequest req)
    {
        var id = await _svc.CrearAsync(req, UsuarioId);
        return CreatedAtAction(nameof(Obtener), new { id }, new { ArticuloID = id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarArticuloRequest req)
    {
        await _svc.ActualizarAsync(id, req);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _svc.EliminarAsync(id);
        return NoContent();
    }
}
