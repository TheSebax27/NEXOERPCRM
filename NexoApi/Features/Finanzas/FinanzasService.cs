using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Finanzas.Dtos;

namespace NexoApi.Features.Finanzas;

public interface IFinanzasService
{
    Task<IEnumerable<GastoItem>> ListarGastosAsync(DateTime? desde, DateTime? hasta, string? categoria, int? centroCostoId);
    Task<int> CrearGastoAsync(CrearGastoRequest request, int usuarioId);
    Task ActualizarGastoAsync(int gastoId, ActualizarGastoRequest request);
    Task EliminarGastoAsync(int gastoId);
    Task<IEnumerable<ResumenGastoCategoria>> ResumenPorCategoriaAsync(DateTime? desde, DateTime? hasta);
    Task<IEnumerable<ResumenGastoMes>> ResumenPorMesAsync(int meses);
}

public class FinanzasService : IFinanzasService
{
    private readonly IDbConnectionFactory _db;
    public FinanzasService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<GastoItem>> ListarGastosAsync(
        DateTime? desde, DateTime? hasta, string? categoria, int? centroCostoId)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT g.GastoID, g.Fecha, g.Categoria, g.Descripcion, g.Monto,
                   g.Proveedor, g.Comprobante,
                   g.CentroCostoID, cc.Nombre AS CentroCosto, g.Notas, g.FechaCreacion
            FROM Finanzas.GastosOperativos g
            LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = g.CentroCostoID
            WHERE (@Desde      IS NULL OR g.Fecha >= @Desde)
              AND (@Hasta      IS NULL OR g.Fecha <= @Hasta)
              AND (@Categoria  IS NULL OR g.Categoria = @Categoria)
              AND (@CentroCostoId IS NULL OR g.CentroCostoID = @CentroCostoId)
            ORDER BY g.Fecha DESC, g.GastoID DESC";

        return await con.QueryAsync<GastoItem>(sql, new { Desde = desde, Hasta = hasta, Categoria = categoria, CentroCostoId = centroCostoId });
    }

    public async Task<int> CrearGastoAsync(CrearGastoRequest r, int usuarioId)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            INSERT INTO Finanzas.GastosOperativos
                (Fecha, Categoria, Descripcion, Monto, Proveedor, Comprobante, CentroCostoID, Notas, CreadoPor)
            OUTPUT INSERTED.GastoID
            VALUES (@Fecha, @Categoria, @Descripcion, @Monto, @Proveedor, @Comprobante, @CentroCostoID, @Notas, @UsuarioId)";

        return await con.ExecuteScalarAsync<int>(sql, new
        {
            r.Fecha, r.Categoria, r.Descripcion, r.Monto,
            r.Proveedor, r.Comprobante, r.CentroCostoID, r.Notas, UsuarioId = usuarioId
        });
    }

    public async Task ActualizarGastoAsync(int gastoId, ActualizarGastoRequest r)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            UPDATE Finanzas.GastosOperativos
            SET Fecha = @Fecha, Categoria = @Categoria, Descripcion = @Descripcion,
                Monto = @Monto, Proveedor = @Proveedor, Comprobante = @Comprobante,
                CentroCostoID = @CentroCostoID, Notas = @Notas
            WHERE GastoID = @GastoID";

        var filas = await con.ExecuteAsync(sql, new
        {
            r.Fecha, r.Categoria, r.Descripcion, r.Monto,
            r.Proveedor, r.Comprobante, r.CentroCostoID, r.Notas, GastoID = gastoId
        });
        if (filas == 0) throw new KeyNotFoundException($"Gasto {gastoId} no encontrado.");
    }

    public async Task EliminarGastoAsync(int gastoId)
    {
        using var con = _db.CreateConnection();
        var filas = await con.ExecuteAsync(
            "DELETE FROM Finanzas.GastosOperativos WHERE GastoID = @GastoID", new { GastoID = gastoId });
        if (filas == 0) throw new KeyNotFoundException($"Gasto {gastoId} no encontrado.");
    }

    public async Task<IEnumerable<ResumenGastoCategoria>> ResumenPorCategoriaAsync(DateTime? desde, DateTime? hasta)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT Categoria, SUM(Monto) AS Total, COUNT(*) AS Cantidad
            FROM Finanzas.GastosOperativos
            WHERE (@Desde IS NULL OR Fecha >= @Desde)
              AND (@Hasta IS NULL OR Fecha <= @Hasta)
            GROUP BY Categoria
            ORDER BY Total DESC";

        return await con.QueryAsync<ResumenGastoCategoria>(sql, new { Desde = desde, Hasta = hasta });
    }

    public async Task<IEnumerable<ResumenGastoMes>> ResumenPorMesAsync(int meses)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT YEAR(Fecha) AS Anio, MONTH(Fecha) AS Mes, SUM(Monto) AS Total
            FROM Finanzas.GastosOperativos
            WHERE Fecha >= DATEADD(MONTH, -@Meses, CAST(GETDATE() AS date))
            GROUP BY YEAR(Fecha), MONTH(Fecha)
            ORDER BY Anio DESC, Mes DESC";

        return await con.QueryAsync<ResumenGastoMes>(sql, new { Meses = meses });
    }
}
