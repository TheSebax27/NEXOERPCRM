using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoApi.Common.Data;

namespace NexoApi.Features.Produccion;

public record MedidaArticuloResponse(
    int ArticuloID,
    decimal? Largo, decimal? Ancho, decimal? Espesor, decimal? Diametro,
    string UnidadMedida, string? Notas);

public record ActualizarMedidaRequest(
    decimal? Largo, decimal? Ancho, decimal? Espesor, decimal? Diametro,
    string UnidadMedida, string? Notas);

[ApiController]
[Route("api/produccion/medidas")]
[Authorize]
public class MedidasArticuloController : ControllerBase
{
    private readonly IDbConnectionFactory _db;

    public MedidasArticuloController(IDbConnectionFactory db)
    {
        _db = db;
    }

    [HttpGet("{articuloId:int}")]
    public async Task<ActionResult<MedidaArticuloResponse>> Obtener(int articuloId)
    {
        using var conn = _db.CreateConnection();
        var fila = await conn.QuerySingleOrDefaultAsync<MedidaArticuloResponse>(
            @"SELECT ArticuloID, Largo, Ancho, Espesor, Diametro, UnidadMedida, Notas
              FROM Produccion.MedidasArticulo WHERE ArticuloID = @ArticuloID",
            new { ArticuloID = articuloId });

        if (fila is null) return NotFound();
        return Ok(fila);
    }

    [HttpPut("{articuloId:int}")]
    public async Task<ActionResult> Guardar(int articuloId, ActualizarMedidaRequest request)
    {
        using var conn = _db.CreateConnection();

        var existe = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM Produccion.MedidasArticulo WHERE ArticuloID = @ArticuloID",
            new { ArticuloID = articuloId });

        if (existe > 0)
        {
            await conn.ExecuteAsync(
                @"UPDATE Produccion.MedidasArticulo
                  SET Largo = @Largo, Ancho = @Ancho, Espesor = @Espesor,
                      Diametro = @Diametro, UnidadMedida = @UnidadMedida, Notas = @Notas
                  WHERE ArticuloID = @ArticuloID",
                new { articuloId, request.Largo, request.Ancho, request.Espesor,
                      request.Diametro, request.UnidadMedida, request.Notas,
                      ArticuloID = articuloId });
        }
        else
        {
            await conn.ExecuteAsync(
                @"INSERT INTO Produccion.MedidasArticulo
                    (ArticuloID, Largo, Ancho, Espesor, Diametro, UnidadMedida, Notas)
                  VALUES (@ArticuloID, @Largo, @Ancho, @Espesor, @Diametro, @UnidadMedida, @Notas)",
                new { ArticuloID = articuloId, request.Largo, request.Ancho, request.Espesor,
                      request.Diametro, request.UnidadMedida, request.Notas });
        }

        return NoContent();
    }
}
