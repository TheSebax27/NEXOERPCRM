using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Data;
using NexoApi.Features.Rrhh.Dtos;

namespace NexoApi.Features.Rrhh;

[ApiController]
[Route("api/rrhh")]
[Authorize]
public class ContratosController : ControllerBase
{
    private readonly IDbConnectionFactory _db;
    private int UsuarioActualId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public ContratosController(IDbConnectionFactory db) => _db = db;

    [HttpGet("empleados/{empleadoId:int}/contratos")]
    public async Task<ActionResult<IEnumerable<ContratoItem>>> Listar(int empleadoId)
    {
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<ContratoItem>(
            @"SELECT ContratoID, EmpleadoID, TipoContrato, FechaInicio, FechaFin,
                     SalarioBase, Notas, Estado, FechaCreacion
              FROM Rrhh.Contratos
              WHERE EmpleadoID = @EmpleadoID
              ORDER BY FechaInicio DESC",
            new { EmpleadoID = empleadoId });
        return Ok(items);
    }

    [HttpPost("empleados/{empleadoId:int}/contratos")]
    public async Task<ActionResult> Crear(int empleadoId, CrearContratoRequest request)
    {
        if (!new[] { "FIJO", "INDEFINIDO", "OBRA_LABOR", "TEMPORAL", "PRACTICAS" }.Contains(request.TipoContrato))
            return BadRequest(new { error = "TipoContrato inválido." });
        if (request.FechaFin.HasValue && request.FechaFin < request.FechaInicio)
            return BadRequest(new { error = "FechaFin no puede ser anterior a FechaInicio." });

        using var conn = _db.CreateConnection();
        var existe = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Rrhh.Empleados WHERE EmpleadoID = @EmpleadoID",
            new { EmpleadoID = empleadoId });
        if (existe == 0) return NotFound(new { error = "Empleado no encontrado." });

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Rrhh.Contratos (EmpleadoID, TipoContrato, FechaInicio, FechaFin, SalarioBase, Notas, UsuarioID)
              VALUES (@EmpleadoID, @TipoContrato, @FechaInicio, @FechaFin, @SalarioBase, @Notas, @UsuarioID);
              SELECT SCOPE_IDENTITY();",
            new { EmpleadoID = empleadoId, request.TipoContrato, request.FechaInicio,
                  request.FechaFin, request.SalarioBase, request.Notas, UsuarioID = UsuarioActualId });
        return Ok(new { contratoId = id });
    }

    [HttpPatch("contratos/{contratoId:int}/estado")]
    public async Task<ActionResult> ActualizarEstado(int contratoId, ActualizarEstadoContratoRequest request)
    {
        if (!new[] { "ACTIVO", "TERMINADO", "NO_RENOVADO" }.Contains(request.Estado))
            return BadRequest(new { error = "Estado inválido. Valores: ACTIVO, TERMINADO, NO_RENOVADO." });

        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            "UPDATE Rrhh.Contratos SET Estado = @Estado WHERE ContratoID = @ContratoID",
            new { request.Estado, ContratoID = contratoId });
        if (filas == 0) return NotFound();
        return NoContent();
    }

    [HttpDelete("contratos/{contratoId:int}")]
    public async Task<ActionResult> Eliminar(int contratoId)
    {
        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            "DELETE FROM Rrhh.Contratos WHERE ContratoID = @ContratoID",
            new { ContratoID = contratoId });
        if (filas == 0) return NotFound();
        return NoContent();
    }
}
