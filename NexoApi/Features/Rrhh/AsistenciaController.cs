using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Rrhh.Dtos;
using QRCoder;

namespace NexoApi.Features.Rrhh;

[ApiController]
[Route("api/rrhh/asistencia")]
[Authorize]
public class AsistenciaController(IAsistenciaService service) : ControllerBase
{
    private int UsuarioActualId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ---- QR (sin autenticacion: la pantalla kiosco no tiene sesion) ----

    [HttpGet("qr-imagen")]
    [AllowAnonymous]
    public async Task<IActionResult> QrImagen([FromQuery] string wb)
    {
        if (string.IsNullOrWhiteSpace(wb)) return BadRequest();
        var resp = await service.ObtenerTokenActualAsync();
        var url = $"{wb.TrimEnd('/')}/rrhh/marcar?t={resp.Token}";
        return File(GenerarPng(url), "image/png");
    }

    [HttpGet("token")]
    [AllowAnonymous]
    public async Task<TokenQrResponse> Token() => await service.ObtenerTokenActualAsync();

    // ---- Configuración del modo QR (solo admin) ----

    [HttpGet("config-qr")]
    [Authorize]
    public async Task<ActionResult<ConfigQrResponse>> ObtenerConfigQr()
        => Ok(await service.ObtenerModoQrAsync());

    [HttpPut("config-qr")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarConfigQr(ActualizarModoQrRequest request)
    {
        try { await service.ActualizarModoQrAsync(request.Modo); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---- Empleado: marcar con QR (requiere login para identificar al empleado) ----

    [HttpGet("estado-hoy")]
    [Authorize]
    public async Task<ActionResult<EstadoAsistenciaHoy>> EstadoHoy()
    {
        try { return Ok(await service.ObtenerEstadoHoyAsync(UsuarioActualId)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("marcar/qr")]
    [Authorize]
    public async Task<ActionResult> MarcarQr(MarcarQrRequest request)
    {
        try { await service.MarcarQrAsync(UsuarioActualId, request); return Ok(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // ---- Admin: marcado manual con advertencia ----

    [HttpPost("marcar/manual")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> MarcarManual(MarcarManualRequest request)
    {
        try { await service.MarcarManualAsync(UsuarioActualId, request); return Ok(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("lista")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<RegistroAsistenciaItem>>> Lista(
        [FromQuery] int? empleadoId, [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta)
    {
        if (!User.IsInRole("Administracion"))
        {
            var empId = await service.ObtenerEmpleadoIdAsync(UsuarioActualId);
            if (empId is null) return Forbid();
            empleadoId = empId;
        }
        return Ok(await service.ListarAsync(empleadoId, desde, hasta));
    }

    // ---- Horarios ----

    [HttpGet("horarios")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<HorarioItem>>> Horarios()
        => Ok(await service.ListarHorariosAsync());

    [HttpPost("horarios")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> CrearHorario(CrearHorarioRequest request)
        => Ok(new { horarioId = await service.CrearHorarioAsync(request) });

    [HttpPost("empleados/{empleadoId:int}/horario")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> AsignarHorario(int empleadoId, AsignarHorarioRequest request)
    {
        try { await service.AsignarHorarioEmpleadoAsync(empleadoId, request); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("horarios/{id:int}")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarHorario(int id, CrearHorarioRequest request)
    {
        try { await service.ActualizarHorarioAsync(id, request); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    [HttpGet("horarios/{id:int}/asignados")]
    [Authorize]
    public async Task<ActionResult> EmpleadosAsignados(int id)
        => Ok(await service.ObtenerEmpleadosAsignadosAsync(id));

    [HttpGet("empleados-sin-horario")]
    [Authorize]
    public async Task<ActionResult> EmpleadosSinHorario()
        => Ok(await service.ListarEmpleadosSinHorarioAsync());

    [HttpDelete("horarios/{id:int}")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> EliminarHorario(int id)
    {
        try { await service.EliminarHorarioAsync(id); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPatch("horarios/{id:int}/toggle-activo")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ToggleActivo(int id)
    {
        await service.ToggleActivoHorarioAsync(id);
        return NoContent();
    }

    // ---- Utilidad ----

    private static byte[] GenerarPng(string texto)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(8);
    }
}
