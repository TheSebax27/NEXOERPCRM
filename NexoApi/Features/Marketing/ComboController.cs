using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Marketing.Dtos;
using System.Security.Claims;

namespace NexoApi.Features.Marketing;

[ApiController]
[Route("api/marketing/combos")]
[Authorize]
public class ComboController(IComboService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? estado)
        => Ok(await service.ListarAsync(estado));

    [HttpGet("activos")]
    public async Task<IActionResult> ListarActivos()
        => Ok(await service.ListarActivosAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var combo = await service.ObtenerAsync(id);
        return combo is null ? NotFound() : Ok(combo);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Crear([FromBody] CrearComboRequest r)
    {
        var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var id = await service.CrearAsync(r, uid);
        return Ok(new { ComboID = id });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarComboRequest r)
    {
        try
        {
            await service.ActualizarAsync(id, r);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpGet("{id:int}/imagen")]
    [AllowAnonymous]
    public async Task<IActionResult> ObtenerImagen(int id)
    {
        var detalle = await service.ObtenerAsync(id);
        if (detalle?.ImagenBase64 is null) return NotFound();
        var bytes = Convert.FromBase64String(detalle.ImagenBase64);
        return File(bytes, detalle.ImagenContentType ?? "image/jpeg");
    }

    [HttpGet("{id:int}/precio-calculado")]
    public async Task<IActionResult> PrecioCalculado(int id)
        => Ok(new { Precio = await service.CalcularPrecioAsync(id) });

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Administracion")]
    public async Task<IActionResult> Eliminar(int id)
    {
        try { await service.EliminarComboAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }
}
