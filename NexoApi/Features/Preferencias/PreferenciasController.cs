using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Preferencias.Dtos;

namespace NexoApi.Features.Preferencias;

[ApiController]
[Route("api/preferencias")]
[Authorize]
public class PreferenciasController : ControllerBase
{
    private readonly IPreferenciasService _service;

    public PreferenciasController(IPreferenciasService service)
    {
        _service = service;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>Todas las preferencias del usuario autenticado (tema, vistas por pantalla, etc.).</summary>
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, string>>> ObtenerTodas()
        => Ok(await _service.ObtenerTodasAsync(UsuarioActualId));

    /// <summary>Crea o actualiza una preferencia puntual (ej. clave "TemaOscuro" o "Vista:articulos").</summary>
    [HttpPut("{clave}")]
    public async Task<ActionResult> Guardar(string clave, GuardarPreferenciaRequest request)
    {
        await _service.GuardarAsync(UsuarioActualId, clave, request.Valor);
        return NoContent();
    }
}
