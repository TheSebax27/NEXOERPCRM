using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Data;

namespace NexoApi.Features.Rrhh;

public record HorasExtrasItem(
    int HorasExtrasID, int EmpleadoID,
    DateTime Fecha, decimal CantidadHoras, decimal ValorHora, decimal Total,
    string? Concepto, DateTime FechaRegistro);

public record CrearHorasExtrasRequest(
    DateTime Fecha, decimal CantidadHoras, decimal ValorHora, string? Concepto);

public record ResumenHorasExtras(
    decimal TotalHoras, decimal TotalPagado, int TotalRegistros);

[ApiController]
[Route("api/rrhh")]
[Authorize]
public class HorasExtrasController : ControllerBase
{
    private readonly IDbConnectionFactory _db;

    public HorasExtrasController(IDbConnectionFactory db)
    {
        _db = db;
    }

    private int UsuarioActualId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("empleados/{empleadoId:int}/horas-extras")]
    public async Task<ActionResult<IEnumerable<HorasExtrasItem>>> Listar(
        int empleadoId,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null)
    {
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<HorasExtrasItem>(
            @"SELECT HorasExtrasID, EmpleadoID, Fecha, CantidadHoras, ValorHora, Total, Concepto, FechaRegistro
              FROM Rrhh.HorasExtras
              WHERE EmpleadoID = @EmpleadoID
                AND (@Desde IS NULL OR Fecha >= @Desde)
                AND (@Hasta IS NULL OR Fecha <= @Hasta)
              ORDER BY Fecha DESC",
            new { EmpleadoID = empleadoId, Desde = desde, Hasta = hasta });
        return Ok(items);
    }

    [HttpGet("empleados/{empleadoId:int}/horas-extras/resumen")]
    public async Task<ActionResult<ResumenHorasExtras>> Resumen(
        int empleadoId,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null)
    {
        using var conn = _db.CreateConnection();
        var resumen = await conn.QuerySingleAsync<ResumenHorasExtras>(
            @"SELECT
                ISNULL(SUM(CantidadHoras), CAST(0 AS DECIMAL(5,2))) AS TotalHoras,
                ISNULL(SUM(Total), CAST(0 AS DECIMAL(18,2))) AS TotalPagado,
                COUNT(1) AS TotalRegistros
              FROM Rrhh.HorasExtras
              WHERE EmpleadoID = @EmpleadoID
                AND (@Desde IS NULL OR Fecha >= @Desde)
                AND (@Hasta IS NULL OR Fecha <= @Hasta)",
            new { EmpleadoID = empleadoId, Desde = desde, Hasta = hasta });
        return Ok(resumen);
    }

    [HttpPost("empleados/{empleadoId:int}/horas-extras")]
    public async Task<ActionResult> Crear(int empleadoId, CrearHorasExtrasRequest request)
    {
        if (request.CantidadHoras <= 0)
            return BadRequest(new { error = "La cantidad de horas debe ser mayor a 0." });
        if (request.ValorHora <= 0)
            return BadRequest(new { error = "El valor por hora debe ser mayor a 0." });

        using var conn = _db.CreateConnection();
        var existe = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Rrhh.Empleados WHERE EmpleadoID = @EmpleadoID",
            new { EmpleadoID = empleadoId });
        if (existe == 0) return NotFound(new { error = "Empleado no encontrado." });

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Rrhh.HorasExtras (EmpleadoID, Fecha, CantidadHoras, ValorHora, Concepto, UsuarioID)
              VALUES (@EmpleadoID, @Fecha, @CantidadHoras, @ValorHora, @Concepto, @UsuarioID);
              SELECT SCOPE_IDENTITY();",
            new { EmpleadoID = empleadoId, request.Fecha, request.CantidadHoras,
                  request.ValorHora, request.Concepto, UsuarioID = UsuarioActualId });

        return CreatedAtAction(nameof(Listar), new { empleadoId }, new { horasExtrasId = id });
    }

    [HttpDelete("horas-extras/{horasExtrasId:int}")]
    public async Task<ActionResult> Eliminar(int horasExtrasId)
    {
        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            "DELETE FROM Rrhh.HorasExtras WHERE HorasExtrasID = @HorasExtrasID",
            new { HorasExtrasID = horasExtrasId });
        if (filas == 0) return NotFound();
        return NoContent();
    }
}
