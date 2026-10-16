using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Soporte.Dtos;
using System.Security.Claims;

namespace NexoApi.Features.Soporte;

[Authorize]
[ApiController]
[Route("api/soporte")]
public class SoporteController : ControllerBase
{
    private readonly ISoporteService _svc;
    public SoporteController(ISoporteService svc) => _svc = svc;

    private int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("tickets")]
    public async Task<IActionResult> Listar(
        [FromQuery] string? estado,
        [FromQuery] string? prioridad,
        [FromQuery] int? asignadoA,
        [FromQuery] int? reportadoPor,
        [FromQuery] int? clienteId)
    {
        var lista = await _svc.ListarTicketsAsync(estado, prioridad, asignadoA, reportadoPor, clienteId);
        return Ok(lista);
    }

    [HttpGet("tickets/{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var ticket = await _svc.ObtenerTicketAsync(id);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HttpPost("tickets")]
    public async Task<IActionResult> Crear([FromBody] CrearTicketRequest request)
    {
        var id = await _svc.CrearTicketAsync(request, UsuarioId);
        return Ok(new { TicketID = id });
    }

    [HttpPut("tickets/{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarTicketRequest request)
    {
        try { await _svc.ActualizarTicketAsync(id, request); return Ok(); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPatch("tickets/{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoTicketRequest request)
    {
        await _svc.CambiarEstadoAsync(id, request.Estado, UsuarioId);
        return Ok();
    }

    [HttpGet("tickets/{id:int}/comentarios")]
    public async Task<IActionResult> ListarComentarios(int id)
    {
        var comentarios = await _svc.ListarComentariosAsync(id);
        return Ok(comentarios);
    }

    [HttpPost("tickets/{id:int}/comentarios")]
    public async Task<IActionResult> AgregarComentario(int id, [FromBody] CrearTicketComentarioRequest request)
    {
        await _svc.AgregarComentarioAsync(id, request, UsuarioId);
        return Ok();
    }

    // NPS
    [HttpGet("tickets/{id:int}/nps")]
    public async Task<IActionResult> ObtenerNps(int id)
        => Ok(await _svc.ObtenerNpsAsync(id));

    [HttpPost("tickets/{id:int}/nps")]
    public async Task<IActionResult> RegistrarNps(int id, [FromBody] RegistrarNpsRequest request)
    {
        await _svc.RegistrarNpsAsync(id, request);
        return Ok();
    }

    [HttpGet("nps/resumen")]
    public async Task<IActionResult> ResumenNps(
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var h = hasta ?? NexoApi.Common.ColombiaClock.Today;
        var d = desde ?? h.AddMonths(-3);
        return Ok(await _svc.ObtenerResumenNpsAsync(d, h));
    }
}
