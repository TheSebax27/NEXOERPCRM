using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NexoApi.Features.Email;

[ApiController]
[Route("api/email")]
[Authorize]
public class EmailController(IEmailService emailService) : ControllerBase
{
    [HttpGet("config")]
    [Authorize(Roles = "Administracion")]
    public async Task<EmailConfigItem> ObtenerConfig() =>
        await emailService.ObtenerConfigAsync();

    [HttpPut("config")]
    [Authorize(Roles = "Administracion")]
    public async Task<IActionResult> GuardarConfig([FromBody] EmailConfigItem config)
    {
        await emailService.GuardarConfigAsync(config);
        return Ok();
    }

    [HttpGet("contador")]
    public async Task<EmailContadorItem> ObtenerContador() =>
        await emailService.ObtenerContadorAsync();

    [HttpPost("test")]
    [Authorize(Roles = "Administracion")]
    public async Task<IActionResult> EnviarTest([FromQuery] string to)
    {
        if (string.IsNullOrWhiteSpace(to))
            return BadRequest(new { error = "Debes indicar un email de destino." });

        var config = await emailService.ObtenerConfigAsync();
        if (!config.Activo || string.IsNullOrWhiteSpace(config.ApiKey))
            return BadRequest(new { error = "El servicio de email no está activo o no tiene API Key configurada." });

        var html = EmailTemplates.Bienvenida("NEXO ERP", "Administrador");
        var ok = await emailService.SendAsync(new EmailMessage(to, "✅ Email de prueba - NEXO ERP", html, "Administrador"));

        return ok
            ? Ok(new { mensaje = $"Email de prueba enviado a {to}." })
            : BadRequest(new { error = "El email no pudo enviarse. Verifica la API Key y el email remitente." });
    }
}
