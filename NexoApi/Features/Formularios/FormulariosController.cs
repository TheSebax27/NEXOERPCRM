using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common;
using NexoApi.Features.Formularios.Dtos;
using System.Security.Claims;

namespace NexoApi.Features.Formularios;

[ApiController]
[Route("api/formularios")]
public class FormulariosController : ControllerBase
{
    private readonly IFormulariosService _svc;
    public FormulariosController(IFormulariosService svc) => _svc = svc;

    private int UsuarioId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Listar()
        => Ok(await _svc.ListarAsync(UsuarioId));

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var f = await _svc.ObtenerAsync(id);
        return f is null ? NotFound() : Ok(f);
    }

    // Endpoint público — no requiere auth
    [AllowAnonymous]
    [HttpGet("publico/{token}")]
    public async Task<IActionResult> ObtenerPublico(string token)
    {
        var f = await _svc.ObtenerPublicoAsync(token);
        return f is null ? NotFound() : Ok(f);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearFormularioRequest request)
    {
        var id = await _svc.CrearAsync(request, UsuarioId);
        return Ok(new { FormularioID = id });
    }

    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActualizarFormularioRequest request)
    {
        await _svc.ActualizarAsync(id, request);
        return Ok();
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await _svc.EliminarAsync(id);
        return Ok();
    }

    [Authorize]
    [HttpGet("{id:int}/campos")]
    public async Task<IActionResult> ListarCampos(int id)
        => Ok(await _svc.ListarCamposAsync(id));

    [Authorize]
    [HttpPost("{id:int}/campos")]
    public async Task<IActionResult> GuardarCampos(int id, [FromBody] GuardarCamposRequest request)
    {
        await _svc.GuardarCamposAsync(id, request);
        return Ok();
    }

    [Authorize]
    [HttpGet("{id:int}/respuestas")]
    public async Task<IActionResult> ListarRespuestas(int id)
        => Ok(await _svc.ListarRespuestasAsync(id));

    // QR del enlace público del formulario
    [AllowAnonymous]
    [HttpGet("{id:int}/qr-png")]
    public async Task<IActionResult> QrPng(int id, [FromQuery] string frontendUrl = "")
    {
        var f = await _svc.ObtenerAsync(id);
        if (f is null) return NotFound();
        var url = string.IsNullOrWhiteSpace(frontendUrl)
            ? $"/f/{f.Token}"
            : $"{frontendUrl.TrimEnd('/')}/f/{f.Token}";
        return File(QrHelper.GenerarPng(url), "image/png");
    }

    // Envío de respuesta — público (cualquiera con el link puede responder)
    [AllowAnonymous]
    [HttpPost("respuestas")]
    public async Task<IActionResult> EnviarRespuesta([FromBody] EnviarRespuestaRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        try
        {
            await _svc.EnviarRespuestaAsync(request, ip);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
