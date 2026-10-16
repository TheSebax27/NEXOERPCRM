using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Busqueda.Dtos;

namespace NexoApi.Features.Busqueda;

[ApiController]
[Route("api/busqueda")]
[Authorize]
public class BusquedaController : ControllerBase
{
    private readonly IBusquedaService _service;

    public BusquedaController(IBusquedaService service)
    {
        _service = service;
    }

    /// <summary>Busqueda global de la barra superior -- agrupa coincidencias por categoria.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ResultadoBusqueda>>> Buscar([FromQuery] string q)
    {
        var resultados = await _service.BuscarAsync(q, User);
        return Ok(resultados);
    }
}
