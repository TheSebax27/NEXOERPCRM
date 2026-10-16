using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Calendario.Dtos;

namespace NexoApi.Features.Calendario;

[ApiController]
[Route("api/calendario")]
[Authorize]
public class CalendarioController : ControllerBase
{
    private readonly ICalendarioService _service;

    public CalendarioController(ICalendarioService service) => _service = service;

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool EsAdminOJefe =>
        User.IsInRole("Administracion") || User.IsInRole("Jefes");

    [HttpGet("eventos")]
    public async Task<ActionResult<IEnumerable<EventoCalendarioItem>>> ListarEventos(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        int? filtroUsuario = EsAdminOJefe ? null : UsuarioActualId;
        return Ok(await _service.ListarEventosAsync(desde, hasta, filtroUsuario));
    }

    [HttpGet("eventos/proximo")]
    public async Task<ActionResult<EventoCalendarioItem?>> ProximoEvento()
        => Ok(await _service.ObtenerProximoEventoAsync(UsuarioActualId, EsAdminOJefe));

    [HttpGet("usuarios")]
    public async Task<ActionResult<IEnumerable<UsuarioDisponibleItem>>> ListarUsuarios()
        => Ok(await _service.ListarUsuariosDisponiblesAsync());

    [HttpPost("eventos")]
    [Authorize]
    public async Task<ActionResult> CrearEvento(CrearEventoRequest request)
    {
        var id = await _service.CrearEventoAsync(request, UsuarioActualId);
        return Ok(new { eventoId = id });
    }

    [HttpPut("eventos/{id:int}")]
    [Authorize]
    public async Task<ActionResult> ActualizarEvento(int id, ActualizarEventoRequest request)
    {
        await _service.ActualizarEventoAsync(id, request);
        return NoContent();
    }

    [HttpDelete("eventos/{id:int}")]
    [Authorize]
    public async Task<ActionResult> EliminarEvento(int id)
    {
        await _service.EliminarEventoAsync(id);
        return NoContent();
    }
}
