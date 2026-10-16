using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Crm.Dtos;
using NexoApi.Features.WhatsApp.Dtos;

namespace NexoApi.Features.WhatsApp;

[ApiController]
[Route("api/whatsapp")]
[Authorize]
public class WhatsAppController(IWhatsAppService svc) : ControllerBase
{
    [HttpGet("config")]
    [Authorize(Roles = "Administracion")]
    public async Task<WhatsAppConfigItem> ObtenerConfig() =>
        await svc.ObtenerConfigAsync();

    [HttpPut("config")]
    [Authorize(Roles = "Administracion")]
    public async Task<IActionResult> GuardarConfig([FromBody] WhatsAppConfigItem config)
    {
        await svc.GuardarConfigAsync(config);
        return Ok();
    }

    [HttpPost("test")]
    [Authorize(Roles = "Administracion")]
    public async Task<IActionResult> EnviarTest([FromQuery] string telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
            return BadRequest(new { error = "Debes indicar un número de teléfono de destino." });

        var config = await svc.ObtenerConfigAsync();
        if (!config.Activo || string.IsNullOrWhiteSpace(config.AccountSid))
            return BadRequest(new { error = "WhatsApp no está activo o no tiene credenciales configuradas." });

        var result = await svc.EnviarAsync(telefono, "✅ Mensaje de prueba desde NEXO ERP — la integración con WhatsApp está funcionando correctamente.");
        return result.Enviado
            ? Ok(new { mensaje = $"Mensaje de prueba enviado al {telefono}." })
            : BadRequest(new { error = result.Error ?? "No se pudo enviar el mensaje." });
    }

    [HttpPost("enviar")]
    public async Task<IActionResult> Enviar([FromBody] EnviarWhatsAppRequest req)
    {
        var result = await svc.EnviarAsync(req.Telefono, req.Mensaje);
        return result.Enviado ? Ok(result) : BadRequest(result);
    }

    // ── Numeros de negocio configurables ─────────────────────────────────────

    [HttpGet("numeros")]
    public async Task<ActionResult<IEnumerable<NumeroWhatsAppItem>>> ListarNumeros()
        => Ok(await svc.ListarNumerosAsync());

    [HttpPost("numeros")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> CrearNumero([FromBody] CrearNumeroWhatsAppRequest req)
    {
        var id = await svc.CrearNumeroAsync(req);
        return Ok(new { NumeroWAID = id });
    }

    [HttpPut("numeros/{id:int}/toggle")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ToggleNumero(int id)
    {
        await svc.ToggleNumeroAsync(id);
        return Ok();
    }

    [HttpDelete("numeros/{id:int}")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> EliminarNumero(int id)
    {
        await svc.EliminarNumeroAsync(id);
        return Ok();
    }

    // Genera URL wa.me para abrir WhatsApp del destinatario (link directo, sin API de pago)
    [HttpGet("link")]
    public ActionResult<object> GenerarLink([FromQuery] string telefono, [FromQuery] string mensaje = "")
    {
        var num = telefono.Trim().TrimStart('+').Replace(" ", "").Replace("-", "");
        var url = string.IsNullOrWhiteSpace(mensaje)
            ? $"https://wa.me/{num}"
            : $"https://wa.me/{num}?text={Uri.EscapeDataString(mensaje)}";
        return Ok(new { url });
    }

    [HttpPost("enviar-masivo")]
    [Authorize(Roles = "Administracion")]
    public async Task<IActionResult> EnviarMasivo([FromBody] EnvioMasivoRequest req)
    {
        if (!req.Destinatarios.Any())
            return BadRequest(new { error = "Debes seleccionar al menos un destinatario." });
        if (string.IsNullOrWhiteSpace(req.Mensaje))
            return BadRequest(new { error = "El mensaje no puede estar vacío." });

        var config = await svc.ObtenerConfigAsync();
        if (!config.Activo || string.IsNullOrWhiteSpace(config.AccountSid))
            return BadRequest(new { error = "WhatsApp no está activo o no tiene credenciales configuradas." });

        var resultados = new List<ResultadoDestinatario>();
        foreach (var d in req.Destinatarios)
        {
            var r = await svc.EnviarAsync(d.Telefono, req.Mensaje);
            resultados.Add(new ResultadoDestinatario(d.ClienteID, d.Nombre, d.Telefono, r.Enviado, r.Error));
            // Pausa mínima para no saturar Twilio
            await Task.Delay(200);
        }

        return Ok(new
        {
            Total       = resultados.Count,
            Enviados    = resultados.Count(r => r.Enviado),
            Fallidos    = resultados.Count(r => !r.Enviado),
            Resultados  = resultados
        });
    }
}
