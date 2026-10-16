using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Data;

namespace NexoApi.Features.Produccion;

public record OrdenEtapaItem(
    int OrdenEtapaID, int OrdenProduccionID, string NombreEtapa, int Orden,
    string Estado, decimal? CantidadRecibida, decimal? CantidadTerminada,
    decimal? CantidadRechazada, int? EmpleadoID, string? NombreEmpleado,
    DateTime? FechaInicio, DateTime? FechaFin, int? TiempoMinutos, string? Notas);

public record InicializarEtapasRequest(List<string>? Etapas);

public record ActualizarEtapaRequest(
    string Estado, decimal? CantidadRecibida, decimal? CantidadTerminada,
    decimal? CantidadRechazada, int? EmpleadoID,
    DateTime? FechaInicio, DateTime? FechaFin, int? TiempoMinutos, string? Notas);

[ApiController]
[Route("api/produccion")]
[Authorize]
public class OrdenEtapaController : ControllerBase
{
    private static readonly string[] EtapasDefault =
        ["CORTE", "MECANIZADO", "ARMADO", "LIJADO", "PINTURA", "DESPACHO"];

    private readonly IDbConnectionFactory _db;
    public OrdenEtapaController(IDbConnectionFactory db) => _db = db;

    [HttpGet("ordenes/{ordenId:int}/etapas")]
    public async Task<ActionResult<IEnumerable<OrdenEtapaItem>>> Listar(int ordenId)
    {
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<OrdenEtapaItem>(
            @"SELECT e.OrdenEtapaID, e.OrdenProduccionID, e.NombreEtapa, e.Orden,
                     e.Estado, e.CantidadRecibida, e.CantidadTerminada, e.CantidadRechazada,
                     e.EmpleadoID,
                     ISNULL(em.Nombres + ' ' + em.Apellidos, CAST(NULL AS NVARCHAR(200))) AS NombreEmpleado,
                     e.FechaInicio, e.FechaFin, e.TiempoMinutos, e.Notas
              FROM Produccion.OrdenEtapa e
              LEFT JOIN Rrhh.Empleados em ON em.EmpleadoID = e.EmpleadoID
              WHERE e.OrdenProduccionID = @OrdenID
              ORDER BY e.Orden",
            new { OrdenID = ordenId });
        return Ok(items);
    }

    [HttpPost("ordenes/{ordenId:int}/etapas/inicializar")]
    public async Task<ActionResult> Inicializar(int ordenId, InicializarEtapasRequest? req)
    {
        using var conn = _db.CreateConnection();
        var existeOrden = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Produccion.OrdenesProduccion WHERE OrdenProduccionID = @ID",
            new { ID = ordenId });
        if (existeOrden == 0) return NotFound(new { error = "Orden no encontrada." });

        var yaExisten = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Produccion.OrdenEtapa WHERE OrdenProduccionID = @ID",
            new { ID = ordenId });
        if (yaExisten > 0)
            return Conflict(new { error = "Las etapas ya fueron inicializadas para esta orden." });

        var etapas = req?.Etapas?.Count > 0 ? req.Etapas : EtapasDefault.ToList();
        for (int i = 0; i < etapas.Count; i++)
        {
            await conn.ExecuteAsync(
                @"INSERT INTO Produccion.OrdenEtapa (OrdenProduccionID, NombreEtapa, Orden, Estado)
                  VALUES (@OrdenProduccionID, @NombreEtapa, @Orden, 'PENDIENTE')",
                new { OrdenProduccionID = ordenId, NombreEtapa = etapas[i], Orden = i + 1 });
        }
        return Ok(new { etapasCreadas = etapas.Count });
    }

    [HttpPatch("etapas/{etapaId:int}")]
    public async Task<ActionResult> Actualizar(int etapaId, ActualizarEtapaRequest req)
    {
        if (!new[] { "PENDIENTE", "EN_PROCESO", "COMPLETADA" }.Contains(req.Estado))
            return BadRequest(new { error = "Estado inválido. Valores: PENDIENTE, EN_PROCESO, COMPLETADA." });

        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            @"UPDATE Produccion.OrdenEtapa
              SET Estado             = @Estado,
                  CantidadRecibida   = @CantidadRecibida,
                  CantidadTerminada  = @CantidadTerminada,
                  CantidadRechazada  = @CantidadRechazada,
                  EmpleadoID         = @EmpleadoID,
                  FechaInicio        = @FechaInicio,
                  FechaFin           = @FechaFin,
                  TiempoMinutos      = @TiempoMinutos,
                  Notas              = @Notas
              WHERE OrdenEtapaID = @OrdenEtapaID",
            new { OrdenEtapaID = etapaId, req.Estado, req.CantidadRecibida,
                  req.CantidadTerminada, req.CantidadRechazada, req.EmpleadoID,
                  req.FechaInicio, req.FechaFin, req.TiempoMinutos, req.Notas });
        if (filas == 0) return NotFound();
        return NoContent();
    }

    [HttpDelete("etapas/{etapaId:int}")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> Eliminar(int etapaId)
    {
        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            "DELETE FROM Produccion.OrdenEtapa WHERE OrdenEtapaID = @ID",
            new { ID = etapaId });
        if (filas == 0) return NotFound();
        return NoContent();
    }

    [HttpGet("etapas/resumen/{ordenId:int}")]
    public async Task<ActionResult> Resumen(int ordenId)
    {
        using var conn = _db.CreateConnection();
        var resumen = await conn.QueryAsync<dynamic>(
            @"SELECT NombreEtapa, Estado,
                     ISNULL(CantidadTerminada, CAST(0 AS DECIMAL(18,4))) AS CantidadTerminada,
                     ISNULL(CantidadRechazada, CAST(0 AS DECIMAL(18,4))) AS CantidadRechazada,
                     TiempoMinutos
              FROM Produccion.OrdenEtapa
              WHERE OrdenProduccionID = @OrdenID
              ORDER BY Orden",
            new { OrdenID = ordenId });
        return Ok(resumen);
    }
}
