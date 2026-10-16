using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Data;

namespace NexoApi.Features.Produccion;

public record DespiecePiezaItem(
    int PiezaID, int RecetaDetalleID, string NombrePieza,
    decimal? Largo, decimal? Ancho, decimal? Espesor,
    string UnidadMedida, decimal Cantidad, string? Notas);

public record CrearDespiecePiezaRequest(
    string NombrePieza, decimal? Largo, decimal? Ancho, decimal? Espesor,
    string UnidadMedida, decimal Cantidad, string? Notas);

public record ActualizarDespiecePiezaRequest(
    string NombrePieza, decimal? Largo, decimal? Ancho, decimal? Espesor,
    string UnidadMedida, decimal Cantidad, string? Notas);

[ApiController]
[Route("api/produccion")]
[Authorize]
public class DespiecePiezasController : ControllerBase
{
    private readonly IDbConnectionFactory _db;
    public DespiecePiezasController(IDbConnectionFactory db) => _db = db;

    [HttpGet("recetas/detalle/{recetaDetalleId:int}/despiece")]
    public async Task<ActionResult<IEnumerable<DespiecePiezaItem>>> Listar(int recetaDetalleId)
    {
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<DespiecePiezaItem>(
            @"SELECT PiezaID, RecetaDetalleID, NombrePieza,
                     Largo, Ancho, Espesor, UnidadMedida, Cantidad, Notas
              FROM Produccion.DespiecePiezas
              WHERE RecetaDetalleID = @RecetaDetalleID
              ORDER BY PiezaID",
            new { RecetaDetalleID = recetaDetalleId });
        return Ok(items);
    }

    [HttpGet("recetas/{recetaId:int}/despiece")]
    public async Task<ActionResult<IEnumerable<DespiecePiezaItem>>> ListarPorReceta(int recetaId)
    {
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<DespiecePiezaItem>(
            @"SELECT p.PiezaID, p.RecetaDetalleID, p.NombrePieza,
                     p.Largo, p.Ancho, p.Espesor, p.UnidadMedida, p.Cantidad, p.Notas
              FROM Produccion.DespiecePiezas p
              JOIN Produccion.RecetaBOM_Detalle d ON d.RecetaDetalleID = p.RecetaDetalleID
              WHERE d.RecetaID = @RecetaID
              ORDER BY d.Orden, p.PiezaID",
            new { RecetaID = recetaId });
        return Ok(items);
    }

    [HttpPost("recetas/detalle/{recetaDetalleId:int}/despiece")]
    public async Task<ActionResult> Crear(int recetaDetalleId, CrearDespiecePiezaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NombrePieza))
            return BadRequest(new { error = "El nombre de la pieza es requerido." });
        if (req.Cantidad <= 0)
            return BadRequest(new { error = "La cantidad debe ser mayor a 0." });
        if (!new[] { "mm", "cm", "m", "in" }.Contains(req.UnidadMedida))
            return BadRequest(new { error = "UnidadMedida inválida. Valores: mm, cm, m, in." });

        using var conn = _db.CreateConnection();
        var existeDetalle = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Produccion.RecetaBOM_Detalle WHERE RecetaDetalleID = @Id",
            new { Id = recetaDetalleId });
        if (existeDetalle == 0) return NotFound(new { error = "Línea de receta no encontrada." });

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Produccion.DespiecePiezas
                (RecetaDetalleID, NombrePieza, Largo, Ancho, Espesor, UnidadMedida, Cantidad, Notas)
              VALUES (@RecetaDetalleID, @NombrePieza, @Largo, @Ancho, @Espesor, @UnidadMedida, @Cantidad, @Notas);
              SELECT SCOPE_IDENTITY();",
            new { RecetaDetalleID = recetaDetalleId, req.NombrePieza, req.Largo, req.Ancho,
                  req.Espesor, req.UnidadMedida, req.Cantidad, req.Notas });
        return Ok(new { piezaId = id });
    }

    [HttpPut("despiece/{piezaId:int}")]
    public async Task<ActionResult> Actualizar(int piezaId, ActualizarDespiecePiezaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.NombrePieza))
            return BadRequest(new { error = "El nombre de la pieza es requerido." });
        if (req.Cantidad <= 0)
            return BadRequest(new { error = "La cantidad debe ser mayor a 0." });

        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            @"UPDATE Produccion.DespiecePiezas
              SET NombrePieza = @NombrePieza, Largo = @Largo, Ancho = @Ancho,
                  Espesor = @Espesor, UnidadMedida = @UnidadMedida,
                  Cantidad = @Cantidad, Notas = @Notas
              WHERE PiezaID = @PiezaID",
            new { PiezaID = piezaId, req.NombrePieza, req.Largo, req.Ancho,
                  req.Espesor, req.UnidadMedida, req.Cantidad, req.Notas });
        if (filas == 0) return NotFound();
        return NoContent();
    }

    [HttpDelete("despiece/{piezaId:int}")]
    public async Task<ActionResult> Eliminar(int piezaId)
    {
        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            "DELETE FROM Produccion.DespiecePiezas WHERE PiezaID = @PiezaID",
            new { PiezaID = piezaId });
        if (filas == 0) return NotFound();
        return NoContent();
    }
}
