using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Configuracion.Dtos;

namespace NexoApi.Features.Configuracion;

[ApiController]
[Route("api/configuracion")]
[Authorize]
public class ConfiguracionController : ControllerBase
{
    private readonly IConfiguracionService _service;

    public ConfiguracionController(IConfiguracionService service)
    {
        _service = service;
    }

    /// <summary>Nombre y logo de la empresa -- lo necesita cualquier usuario autenticado (se muestra en el sidebar).</summary>
    [HttpGet("empresa")]
    public async Task<ActionResult<ConfiguracionEmpresaResponse>> ObtenerEmpresa()
        => Ok(await _service.ObtenerEmpresaAsync());

    [HttpPut("empresa")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarNombre(ActualizarNombreEmpresaRequest request)
    {
        await _service.ActualizarNombreEmpresaAsync(request.NombreEmpresa, request.NombrePropietario);
        return NoContent();
    }

    [HttpPut("empresa/logo")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarLogo(ActualizarLogoEmpresaRequest request)
    {
        if (Convert.FromBase64String(request.Base64).Length > 1_000_000)
            return BadRequest(new { error = "La imagen es muy grande (máximo 1 MB)." });

        await _service.ActualizarLogoEmpresaAsync(request.Base64, request.ContentType);
        return NoContent();
    }

    [HttpDelete("empresa/logo")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> EliminarLogo()
    {
        await _service.EliminarLogoEmpresaAsync();
        return NoContent();
    }

    [HttpPut("empresa/visions")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarUsaVisions(ActualizarUsaVisionsRequest request)
    {
        await _service.ActualizarUsaVisionsAsync(request.UsaVisions);
        return NoContent();
    }

    [HttpPut("empresa/inventario")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarConfigInventario(ActualizarConfigInventarioRequest request)
    {
        try
        {
            await _service.ActualizarConfigInventarioAsync(
                request.ManejarVencimientos, request.DiasAlertaVencimiento, request.ModoLotes);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("empresa/nrodoc")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<ConfigNroDocResponse>> ObtenerConfigNroDoc()
        => Ok(await _service.ObtenerConfigNroDocAsync());

    [HttpPut("empresa/nrodoc")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarConfigNroDoc(ActualizarConfigNroDocRequest request)
    {
        try
        {
            await _service.ActualizarConfigNroDocAsync(request.ModoNroDoc, request.UltimoNroDocSecuencial);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("empresa/venta-receta")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarVentaDesdeReceta(ActualizarVentaDesdeRecetaRequest request)
    {
        await _service.ActualizarVentaDesdeRecetaAsync(request.VentaDesdeReceta);
        return NoContent();
    }

    [HttpPut("empresa/sistema-medidas")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarSistemaMedidas(ActualizarSistemaMedidasRequest request)
    {
        await _service.ActualizarSistemaMedidasAsync(request.SistemaMedidas);
        return NoContent();
    }

    [HttpPut("empresa/vender-sin-stock")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarVenderSinStock(ActualizarVenderSinStockRequest request)
    {
        await _service.ActualizarVenderSinStockAsync(request.VenderSinStock);
        return NoContent();
    }
}
