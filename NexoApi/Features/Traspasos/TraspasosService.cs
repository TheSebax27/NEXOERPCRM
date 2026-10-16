using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Traspasos.Dtos;
using System.Data;
using System.Text.Json;

namespace NexoApi.Features.Traspasos;

public interface ITraspasosService
{
    Task<(int TraspasoID, string Codigo)> CrearYEnviarAsync(CrearTraspasoRequest request, int usuarioId);
    Task RecibirAsync(int traspasoId, int usuarioId);
    Task<IEnumerable<TraspasoResumen>> ListarAsync(string? estado);
    Task<IEnumerable<TraspasoDetalleItem>> ObtenerDetalleAsync(int traspasoId);
}

public class TraspasosService : ITraspasosService
{
    private record CrearResultado(string Resultado, int TraspasoID, string Codigo);

    private readonly IDbConnectionFactory _db;

    public TraspasosService(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<(int, string)> CrearYEnviarAsync(CrearTraspasoRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        // Se convierte de c# al formato JSON para enviarlo al procedimiento almacenado
        var detalleJson = JsonSerializer.Serialize(r.Detalle);

        var parametros = new DynamicParameters();
        parametros.Add("BodegaOrigenID", r.BodegaOrigenID);
        parametros.Add("BodegaDestinoID", r.BodegaDestinoID);
        parametros.Add("UsuarioEnviaID", usuarioId);
        parametros.Add("DetalleJSON", detalleJson);

        var resultado = await connection.QuerySingleAsync<CrearResultado>(
            "Inventario.sp_CrearYEnviarTraspaso",
            parametros,
            commandType: CommandType.StoredProcedure);

        return (resultado.TraspasoID, resultado.Codigo);
    }

    public async Task RecibirAsync(int traspasoId, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        var parametros = new DynamicParameters();
        parametros.Add("TraspasoID", traspasoId);
        parametros.Add("UsuarioRecibeID", usuarioId);

        await connection.ExecuteAsync(
            "Inventario.sp_RecibirTraspaso",
            parametros,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<TraspasoResumen>> ListarAsync(string? estado)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT t.TraspasoID, t.Codigo, bo.Nombre AS BodegaOrigen, bd.Nombre AS BodegaDestino,
                   t.EstadoTraspaso, t.FechaEnvio, t.FechaRecepcion
            FROM Inventario.TraspasosBodega t
            JOIN Inventario.Bodegas bo ON bo.BodegaID = t.BodegaOrigenID
            JOIN Inventario.Bodegas bd ON bd.BodegaID = t.BodegaDestinoID
            WHERE (@Estado IS NULL OR t.EstadoTraspaso = @Estado)
              AND t.FechaCreacion >= DATEADD(MONTH, -6, SYSUTCDATETIME())
            ORDER BY t.FechaCreacion DESC
            OFFSET 0 ROWS FETCH NEXT 500 ROWS ONLY";

        return await connection.QueryAsync<TraspasoResumen>(sql, new { Estado = estado });
    }

    public async Task<IEnumerable<TraspasoDetalleItem>> ObtenerDetalleAsync(int traspasoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT d.TraspasoDetalleID, d.ArticuloID, a.Nombre AS Articulo,
                   d.CantidadEnviada, d.CantidadRecibida, d.CostoUnitario
            FROM Inventario.TraspasosDetalle d
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = d.ArticuloID
            WHERE d.TraspasoID = @TraspasoId";

        return await connection.QueryAsync<TraspasoDetalleItem>(sql, new { TraspasoId = traspasoId });
    }
}