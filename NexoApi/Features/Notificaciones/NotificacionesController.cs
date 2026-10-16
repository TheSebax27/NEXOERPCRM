using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Notificaciones.Dtos;

namespace NexoApi.Features.Notificaciones;

[ApiController]
[Route("api/notificaciones")]
[Authorize]
public class NotificacionesController : ControllerBase
{
    private readonly INotificacionesService _service;

    public NotificacionesController(INotificacionesService service)
    {
        _service = service;
    }

    [HttpGet("resumen")]
    public async Task<ActionResult<ResumenNotificaciones>> ObtenerResumen()
        => Ok(await _service.ObtenerResumenAsync(User));
}
