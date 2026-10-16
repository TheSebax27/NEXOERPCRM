using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Data;

namespace NexoApi.Features.Produccion;

public record SobranteMaterialItem(
    int SobranteID, int? OrdenProduccionID, string? CodigoOP,
    int ArticuloID, string NombreArticulo,
    decimal Largo, decimal Ancho, decimal Espesor,
    string UnidadMedida, int CantidadPiezas, string Estado,
    int? BodegaID, string? NombreBodega, string? Notas, DateTime FechaRegistro);

public record CrearSobranteRequest(
    int? OrdenProduccionID, int ArticuloID,
    decimal Largo, decimal Ancho, decimal Espesor,
    string UnidadMedida, int CantidadPiezas,
    int? BodegaID, string? Notas);

public record ActualizarEstadoSobranteRequest(string Estado);

[ApiController]
[Route("api/produccion/sobrantes")]
[Authorize]
public class SobranteMaterialController : ControllerBase
{
    private readonly IDbConnectionFactory _db;
    private int UsuarioActualId =>
        int.TryParse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public SobranteMaterialController(IDbConnectionFactory db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SobranteMaterialItem>>> Listar(
        [FromQuery] string? estado = "DISPONIBLE",
        [FromQuery] int? articuloId = null,
        [FromQuery] int? bodegaId = null,
        [FromQuery] int? ordenId = null)
    {
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<SobranteMaterialItem>(
            @"SELECT s.SobranteID, s.OrdenProduccionID,
                     ISNULL(op.CodigoOP, CAST(NULL AS NVARCHAR(30))) AS CodigoOP,
                     s.ArticuloID,
                     ISNULL(t.Nombre, CAST(NULL AS NVARCHAR(150))) AS NombreArticulo,
                     s.Largo, s.Ancho, s.Espesor, s.UnidadMedida,
                     s.CantidadPiezas, s.Estado,
                     s.BodegaID,
                     ISNULL(b.Nombre, CAST(NULL AS NVARCHAR(100))) AS NombreBodega,
                     s.Notas, s.FechaRegistro
              FROM Produccion.SobranteMaterial s
              JOIN Catalogo.Tarjetas t ON t.ArticuloID = s.ArticuloID
              LEFT JOIN Produccion.OrdenesProduccion op ON op.OrdenProduccionID = s.OrdenProduccionID
              LEFT JOIN Inventario.Bodegas b ON b.BodegaID = s.BodegaID
              WHERE (@Estado IS NULL OR s.Estado = @Estado)
                AND (@ArticuloID IS NULL OR s.ArticuloID = @ArticuloID)
                AND (@BodegaID IS NULL OR s.BodegaID = @BodegaID)
                AND (@OrdenID IS NULL OR s.OrdenProduccionID = @OrdenID)
              ORDER BY s.FechaRegistro DESC",
            new { Estado = estado, ArticuloID = articuloId, BodegaID = bodegaId, OrdenID = ordenId });
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult> Crear(CrearSobranteRequest req)
    {
        if (!new[] { "mm", "cm", "m", "in" }.Contains(req.UnidadMedida))
            return BadRequest(new { error = "UnidadMedida inválida. Valores: mm, cm, m, in." });
        if (req.Largo <= 0 || req.Ancho <= 0 || req.Espesor <= 0)
            return BadRequest(new { error = "Dimensiones deben ser mayores a 0." });
        if (req.CantidadPiezas < 1)
            return BadRequest(new { error = "Cantidad de piezas debe ser al menos 1." });

        using var conn = _db.CreateConnection();
        var existeArticulo = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Catalogo.Tarjetas WHERE ArticuloID = @ID",
            new { ID = req.ArticuloID });
        if (existeArticulo == 0) return NotFound(new { error = "Artículo no encontrado." });

        var id = await conn.ExecuteScalarAsync<int>(
            @"INSERT INTO Produccion.SobranteMaterial
                (OrdenProduccionID, ArticuloID, Largo, Ancho, Espesor, UnidadMedida,
                 CantidadPiezas, Estado, BodegaID, Notas, UsuarioID)
              VALUES (@OrdenProduccionID, @ArticuloID, @Largo, @Ancho, @Espesor, @UnidadMedida,
                      @CantidadPiezas, 'DISPONIBLE', @BodegaID, @Notas, @UsuarioID);
              SELECT SCOPE_IDENTITY();",
            new { req.OrdenProduccionID, req.ArticuloID, req.Largo, req.Ancho, req.Espesor,
                  req.UnidadMedida, req.CantidadPiezas, req.BodegaID, req.Notas,
                  UsuarioID = UsuarioActualId });
        return Ok(new { sobranteId = id });
    }

    [HttpPatch("{sobranteId:int}/estado")]
    public async Task<ActionResult> ActualizarEstado(int sobranteId, ActualizarEstadoSobranteRequest req)
    {
        if (!new[] { "DISPONIBLE", "USADO", "DESECHADO" }.Contains(req.Estado))
            return BadRequest(new { error = "Estado inválido. Valores: DISPONIBLE, USADO, DESECHADO." });

        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            "UPDATE Produccion.SobranteMaterial SET Estado = @Estado WHERE SobranteID = @ID",
            new { Estado = req.Estado, ID = sobranteId });
        if (filas == 0) return NotFound();
        return NoContent();
    }

    [HttpDelete("{sobranteId:int}")]
    [Authorize(Roles = "Administracion")]
    public async Task<ActionResult> Eliminar(int sobranteId)
    {
        using var conn = _db.CreateConnection();
        var filas = await conn.ExecuteAsync(
            "DELETE FROM Produccion.SobranteMaterial WHERE SobranteID = @ID",
            new { ID = sobranteId });
        if (filas == 0) return NotFound();
        return NoContent();
    }

    [HttpGet("resumen")]
    public async Task<ActionResult> Resumen()
    {
        using var conn = _db.CreateConnection();
        var resumen = await conn.QueryAsync<dynamic>(
            @"SELECT s.Estado,
                     COUNT(1) AS TotalRegistros,
                     SUM(s.CantidadPiezas) AS TotalPiezas,
                     SUM(s.Largo * s.Ancho * s.Espesor / 1000000000.0 * s.CantidadPiezas) AS VolumenM3
              FROM Produccion.SobranteMaterial s
              GROUP BY s.Estado",
            new { });
        return Ok(resumen);
    }
}
