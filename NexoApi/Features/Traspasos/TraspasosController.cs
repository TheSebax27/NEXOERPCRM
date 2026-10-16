using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Traspasos.Dtos;

namespace NexoApi.Features.Traspasos;

[ApiController]
[Route("api/inventario/traspasos")]
[Authorize]
public class TraspasosController : ControllerBase
{
    private readonly ITraspasosService _service;

    public TraspasosController(ITraspasosService service)
    {
        _service = service;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TraspasoResumen>>> Listar([FromQuery] string? estado)
    {
        var traspasos = await _service.ListarAsync(estado);
        return Ok(traspasos);
    }

    [HttpGet("{id:int}/detalle")]
    public async Task<ActionResult<IEnumerable<TraspasoDetalleItem>>> ObtenerDetalle(int id)
    {
        var detalle = await _service.ObtenerDetalleAsync(id);
        return Ok(detalle);
    }

    /// <summary>Crea el traspaso y descuenta de inmediato la bodega de origen (queda EN_TRANSITO).</summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult> CrearYEnviar(CrearTraspasoRequest request)
    {
        var (traspasoId, codigo) = await _service.CrearYEnviarAsync(request, UsuarioActualId);
        return CreatedAtAction(nameof(ObtenerDetalle), new { id = traspasoId }, new { TraspasoID = traspasoId, Codigo = codigo });
    }

    /// <summary>Confirma la recepcion en la bodega destino y suma el stock alla.</summary>
    [HttpPost("{id:int}/recibir")]
    [Authorize]
    public async Task<ActionResult> Recibir(int id)
    {
        await _service.RecibirAsync(id, UsuarioActualId);
        return Ok(new { mensaje = "Traspaso recibido, stock actualizado en bodega destino." });
    }
}