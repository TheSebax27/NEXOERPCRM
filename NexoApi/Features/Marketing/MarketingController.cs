using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Marketing.Dtos;

namespace NexoApi.Features.Marketing;

[ApiController]
[Route("api/marketing")]
[Authorize]
public class MarketingController(IMarketingService svc, IConfiguration config) : ControllerBase
{
    // El tracking URL debe apuntar al Web del tenant (que lo proxea al API con X-Nexo-Host).
    // X-Nexo-Host contiene el host del browser (p.ej. veccox.insumar.com.co);
    // como fallback usa ApiBaseUrl del appsettings (solo válido si la API tiene dominio propio por tenant).
    private string TrackingBaseUrl
    {
        get
        {
            var tenantHost = Request.Headers["X-Nexo-Host"].FirstOrDefault();
            return !string.IsNullOrEmpty(tenantHost)
                ? $"https://{tenantHost}"
                : config["ApiBaseUrl"] ?? "";
        }
    }

    // ── Campañas ──────────────────────────────────────────────────────────────

    [HttpGet("campanas")]
    public async Task<IActionResult> Listar() =>
        Ok(await svc.ListarCampanasAsync());

    [HttpGet("campanas/{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var c = await svc.ObtenerCampanaAsync(id);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost("campanas")]
    public async Task<IActionResult> Crear([FromBody] GuardarCampanaRequest r)
    {
        var uid = ObtenerUsuarioId();
        var id  = await svc.CrearCampanaAsync(r, uid);
        return Ok(new { CampanaID = id });
    }

    [HttpPut("campanas/{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GuardarCampanaRequest r)
    {
        try   { await svc.ActualizarCampanaAsync(id, r); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("campanas/{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await svc.EliminarCampanaAsync(id);
        return NoContent();
    }

    // ── Segmentación ──────────────────────────────────────────────────────────

    [HttpGet("campanas/destinatarios/contar")]
    public async Task<IActionResult> ContarDestinatarios(
        [FromQuery] string? tipo, [FromQuery] string? valor)
    {
        var total = await svc.ContarDestinatariosAsync(tipo, valor);
        return Ok(new ContarDestinatariosResponse(total));
    }

    // ── Preview ───────────────────────────────────────────────────────────────

    [HttpPost("campanas/preview")]
    public async Task<IActionResult> Preview([FromBody] PreviewRequest r)
    {
        var empresa = await svc.ObtenerNombreEmpresaAsync();
        var html = CampanaEmailBuilder.Preview(r.BloqueJSON, empresa, r.NombreCliente);
        return Ok(new { Html = html });
    }

    // ── Envío ─────────────────────────────────────────────────────────────────

    [HttpPost("campanas/{id:int}/enviar-prueba")]
    public async Task<IActionResult> EnviarPrueba(int id, [FromBody] EnviarPruebaRequest r)
    {
        try
        {
            var msg = await svc.EnviarPruebaAsync(id, r.EmailDestino);
            return Ok(new { Mensaje = msg });
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("campanas/{id:int}/enviar")]
    public async Task<IActionResult> Enviar(int id)
    {
        try
        {
            await svc.IniciarEnvioAsync(id, TrackingBaseUrl);
            return Ok(new { Mensaje = "Envío iniciado en segundo plano." });
        }
        catch (KeyNotFoundException ex)         { return NotFound(ex.Message); }
        catch (InvalidOperationException ex)    { return BadRequest(ex.Message); }
    }

    // ── Detalle por destinatario ───────────────────────────────────────────────

    [HttpGet("campanas/{id:int}/envios")]
    public async Task<IActionResult> ListarEnvios(int id) =>
        Ok(await svc.ListarEnviosDetalleAsync(id));

    // ── Imágenes ──────────────────────────────────────────────────────────────

    [HttpGet("imagenes")]
    public async Task<IActionResult> ListarImagenes() =>
        Ok(await svc.ListarImagenesAsync());

    [HttpPost("imagenes")]
    public async Task<IActionResult> SubirImagen([FromBody] SubirImagenRequest r)
    {
        var uid = ObtenerUsuarioId();
        var id  = await svc.SubirImagenAsync(r, uid);
        return Ok(new { ImagenID = id });
    }

    [HttpGet("imagenes/{id:int}")]
    public async Task<IActionResult> ObtenerImagen(int id)
    {
        var img = await svc.ObtenerImagenAsync(id);
        if (img is null) return NotFound();
        return Ok(new { ContentType = img.Value.ContentType, Base64 = img.Value.Base64 });
    }

    [HttpDelete("imagenes/{id:int}")]
    public async Task<IActionResult> EliminarImagen(int id)
    {
        await svc.EliminarImagenAsync(id);
        return NoContent();
    }

    // ── Tracking (públicos, sin autenticación) ────────────────────────────────

    [AllowAnonymous]
    [HttpGet("track/open/{token}")]
    public async Task<IActionResult> TrackOpen(string token)
    {
        await svc.RegistrarAperturaAsync(token);
        // Devuelve un GIF 1x1 transparente
        var gif = Convert.FromBase64String(
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");
        return File(gif, "image/gif");
    }

    [AllowAnonymous]
    [HttpGet("track/click/{token}")]
    public async Task<IActionResult> TrackClick(string token, [FromQuery] string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return BadRequest();
        var destino = await svc.RegistrarClickAsync(token, url);
        if (destino is null) return NotFound();
        return Redirect(destino);
    }

    [AllowAnonymous]
    [HttpGet("unsub/{token}")]
    public async Task<IActionResult> Unsub(string token)
    {
        var ok = await svc.RegistrarDesuscripcionAsync(token);
        var msg = ok
            ? "<h2>Listo</h2><p>Fuiste eliminado de nuestra lista de correos.</p>"
            : "<h2>Enlace inválido</h2><p>Este enlace no es válido o ya fue usado.</p>";
        return Content($"<!DOCTYPE html><html><body style='font-family:sans-serif;padding:40px;max-width:500px;margin:auto'>{msg}</body></html>",
            "text/html");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int ObtenerUsuarioId()
    {
        var claim = User.FindFirst("UsuarioID") ?? User.FindFirst("sub");
        return int.TryParse(claim?.Value, out var id) ? id : 0;
    }
}
