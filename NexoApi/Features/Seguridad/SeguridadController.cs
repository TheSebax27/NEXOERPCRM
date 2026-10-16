using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Features.Seguridad.Dtos;

namespace NexoApi.Features.Seguridad;

[ApiController]
[Authorize]
public class SeguridadController : ControllerBase
{
    private readonly ISeguridadService _svc;

    public SeguridadController(ISeguridadService svc) => _svc = svc;

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ── Modulos del usuario actual (NavMenu) ─────────────────────────────────

    [HttpGet("api/seguridad/modulos/mis-modulos")]
    public async Task<ActionResult<IEnumerable<string>>> MisModulos()
        => Ok(await _svc.ObtenerModulosVisiblesUsuarioAsync(UsuarioActualId));

    // ── Catalogo completo (solo admin) ───────────────────────────────────────

    [HttpGet("api/seguridad/modulos")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<IEnumerable<ModuloItem>>> ListarModulos()
        => Ok(await _svc.ListarModulosAsync());

    // ── Visibilidad por rol ──────────────────────────────────────────────────

    [HttpGet("api/seguridad/roles/{rolId:int}/modulos")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<IEnumerable<ModuloVisibilidadItem>>> VisibilidadRol(int rolId)
        => Ok(await _svc.ObtenerVisibilidadRolAsync(rolId));

    [HttpPut("api/seguridad/roles/{rolId:int}/modulos")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarVisibilidadRol(int rolId, ActualizarVisibilidadRolRequest request)
    {
        await _svc.ActualizarVisibilidadRolAsync(rolId, request.Items);
        return Ok(new { mensaje = "Visibilidad de modulos actualizada." });
    }

    // ── Visibilidad por usuario ──────────────────────────────────────────────

    [HttpGet("api/seguridad/usuarios/{usuarioId:int}/modulos")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<IEnumerable<ModuloVisibilidadItem>>> VisibilidadUsuario(int usuarioId)
        => Ok(await _svc.ObtenerVisibilidadUsuarioAsync(usuarioId));

    [HttpPut("api/seguridad/usuarios/{usuarioId:int}/modulos")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarVisibilidadUsuario(int usuarioId, ActualizarVisibilidadUsuarioRequest request)
    {
        await _svc.ActualizarVisibilidadUsuarioAsync(usuarioId, request.Items);
        return Ok(new { mensaje = "Overrides de usuario actualizados." });
    }

    [HttpDelete("api/seguridad/usuarios/{usuarioId:int}/modulos")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> LimpiarOverridesUsuario(int usuarioId)
    {
        await _svc.LimpiarOverridesUsuarioAsync(usuarioId);
        return Ok(new { mensaje = "Overrides eliminados." });
    }

    // ── CRUD de Roles ────────────────────────────────────────────────────────

    [HttpGet("api/seguridad/roles")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult<IEnumerable<RolDetalleItem>>> ListarRoles()
        => Ok(await _svc.ListarRolesAsync());

    [HttpPost("api/seguridad/roles")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> CrearRol(CrearRolRequest request)
    {
        var id = await _svc.CrearRolAsync(request);
        return Ok(new { RolID = id });
    }

    [HttpPut("api/seguridad/roles/{rolId:int}")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> ActualizarRol(int rolId, ActualizarRolRequest request)
    {
        await _svc.ActualizarRolAsync(rolId, request);
        return Ok(new { mensaje = "Rol actualizado." });
    }

    [HttpDelete("api/seguridad/roles/{rolId:int}")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> EliminarRol(int rolId)
    {
        await _svc.EliminarRolAsync(rolId);
        return Ok(new { mensaje = "Rol eliminado." });
    }
}
