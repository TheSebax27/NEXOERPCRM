using System.Data;
using System.Text.Json;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Logistica.Dtos;

namespace NexoApi.Features.Logistica;

public interface ILogisticaService
{
    Task<IEnumerable<DespachoItem>> ListarDespachosAsync(int? centroCostoId, string? estado);
    Task<IEnumerable<DespachoDetalleItem>> ObtenerDetalleAsync(int despachoId);
    Task<int> CrearDespachoAsync(CrearDespachoRequest request, int usuarioId);
    Task ConfirmarEntregaAsync(int despachoId);
    Task AnularDespachoAsync(int despachoId, AnularDespachoRequest request, int usuarioId);
}

public class LogisticaService : ILogisticaService
{
    private readonly IDbConnectionFactory _db;

    public LogisticaService(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<IEnumerable<DespachoItem>> ListarDespachosAsync(int? centroCostoId, string? estado)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT d.DespachoID, 'GUIA-' + RIGHT('000000' + CAST(d.DespachoID AS VARCHAR(6)), 6) AS NumeroGuia,
                   d.ClienteID, c.Nombre AS Cliente, d.CentroCostoID, cc.Nombre AS CentroCosto, b.Nombre AS BodegaOrigen,
                   d.FechaDespacho, d.Estado, d.FechaEntrega, d.Direccion, d.Observaciones,
                   ISNULL((
                       SELECT SUM(dd.Cantidad * ISNULL(dd.ValorUnitario, a.PPublico))
                       FROM Logistica.DespachoDetalle dd
                       JOIN Catalogo.Tarjetas a ON a.ArticuloID = dd.ArticuloID
                       WHERE dd.DespachoID = d.DespachoID
                   ), 0) AS ValorTotal,
                   d.MotivoAnulacion, d.FechaAnulacion, d.DescuentaStock
            FROM Logistica.Despachos d
            JOIN Crm.Clientes c ON c.ClienteID = d.ClienteID
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = d.CentroCostoID
            JOIN Inventario.Bodegas b ON b.BodegaID = d.BodegaOrigenID
            WHERE (@CentroCostoId IS NULL OR d.CentroCostoID = @CentroCostoId)
              AND (@Estado IS NULL OR d.Estado = @Estado)
            ORDER BY d.FechaDespacho DESC";

        return await connection.QueryAsync<DespachoItem>(sql, new { CentroCostoId = centroCostoId, Estado = estado });
    }

    public async Task<IEnumerable<DespachoDetalleItem>> ObtenerDetalleAsync(int despachoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT dd.ArticuloID, a.Referencia AS SkuArticulo, a.Nombre AS NombreArticulo, dd.Cantidad, dd.ValorUnitario
            FROM Logistica.DespachoDetalle dd
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = dd.ArticuloID
            WHERE dd.DespachoID = @DespachoId";

        return await connection.QueryAsync<DespachoDetalleItem>(sql, new { DespachoId = despachoId });
    }

    public async Task<int> CrearDespachoAsync(CrearDespachoRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        var lineasJson = JsonSerializer.Serialize(r.Lineas);

        var parametros = new DynamicParameters();
        parametros.Add("ClienteID", r.ClienteID);
        parametros.Add("CentroCostoID", r.CentroCostoID);
        parametros.Add("BodegaOrigenID", r.BodegaOrigenID);
        parametros.Add("UsuarioID", usuarioId);
        parametros.Add("Direccion", r.Direccion);
        parametros.Add("Observaciones", r.Observaciones);
        parametros.Add("LineasJson", lineasJson);
        parametros.Add("DescuentaStock", r.DescuentaStock);

        return await connection.QuerySingleAsync<int>(
            "Logistica.sp_CrearDespacho", parametros, commandType: CommandType.StoredProcedure);
    }

    public async Task ConfirmarEntregaAsync(int despachoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            UPDATE Logistica.Despachos
            SET Estado = 'ENTREGADO', FechaEntrega = SYSUTCDATETIME()
            WHERE DespachoID = @DespachoId AND Estado = 'DESPACHADO'";

        var filas = await connection.ExecuteAsync(sql, new { DespachoId = despachoId });

        if (filas == 0)
            throw new KeyNotFoundException($"El despacho {despachoId} no existe o ya fue marcado como entregado.");
    }

    public async Task AnularDespachoAsync(int despachoId, AnularDespachoRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();

        var parametros = new DynamicParameters();
        parametros.Add("DespachoID", despachoId);
        parametros.Add("UsuarioID", usuarioId);
        parametros.Add("Motivo", r.Motivo);

        await connection.ExecuteAsync("Logistica.sp_AnularDespacho", parametros, commandType: CommandType.StoredProcedure);
    }
}
