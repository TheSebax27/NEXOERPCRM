using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Crm.Dtos;

namespace NexoApi.Features.Comisiones;

public interface IComisionesService
{
    Task<IEnumerable<ConfigComisionItem>> ListarConfigAsync();
    Task GuardarConfigAsync(GuardarConfigComisionRequest req);
    Task<DashboardComisionItem?> ObtenerDashboardAsync(int empleadoId, int mes, int anio);
    Task<IEnumerable<ResumenComisionesItem>> ResumenTodosAsync(int mes, int anio);
}

public class ComisionesService(IDbConnectionFactory db) : IComisionesService
{
    public async Task<IEnumerable<ConfigComisionItem>> ListarConfigAsync()
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<ConfigComisionItem>(@"
            SELECT c.ConfigID, c.EmpleadoID,
                   e.Nombres + ' ' + e.Apellidos AS NombreEmpleado,
                   c.PorcentajeBase, c.MetaMensual, c.BonoPorMeta, c.Activo
            FROM Rrhh.ConfigComision c
            JOIN Rrhh.Empleados e ON e.EmpleadoID = c.EmpleadoID
            ORDER BY e.Nombres");
    }

    public async Task GuardarConfigAsync(GuardarConfigComisionRequest req)
    {
        using var conn = db.CreateConnection();
        await conn.ExecuteAsync(@"
            IF EXISTS (SELECT 1 FROM Rrhh.ConfigComision WHERE EmpleadoID = @EmpleadoID)
                UPDATE Rrhh.ConfigComision
                SET PorcentajeBase = @PorcentajeBase,
                    MetaMensual    = @MetaMensual,
                    BonoPorMeta    = @BonoPorMeta,
                    Activo         = 1
                WHERE EmpleadoID = @EmpleadoID
            ELSE
                INSERT INTO Rrhh.ConfigComision (EmpleadoID, PorcentajeBase, MetaMensual, BonoPorMeta)
                VALUES (@EmpleadoID, @PorcentajeBase, @MetaMensual, @BonoPorMeta)",
            req);
    }

    public async Task<DashboardComisionItem?> ObtenerDashboardAsync(int empleadoId, int mes, int anio)
    {
        using var conn = db.CreateConnection();

        var config = await conn.QuerySingleOrDefaultAsync<ConfigRow>(@"
            SELECT PorcentajeBase, MetaMensual, BonoPorMeta
            FROM Rrhh.ConfigComision
            WHERE EmpleadoID = @EmpleadoID AND Activo = 1",
            new { EmpleadoID = empleadoId });

        if (config is null) return null;

        var ventas = await conn.QuerySingleAsync<VentasRow>(@"
            SELECT ISNULL(SUM(l.Cantidad * l.PrecioUnitario), 0) AS TotalVentas,
                   COUNT(DISTINCT f.FacturaID)                   AS TotalFacturas
            FROM Facturacion.Facturas f
            LEFT JOIN Facturacion.FacturaLineas l ON l.FacturaID = f.FacturaID
            WHERE f.VendedorID = @EmpleadoID
              AND MONTH(f.Fecha) = @Mes
              AND YEAR(f.Fecha)  = @Anio",
            new { EmpleadoID = empleadoId, Mes = mes, Anio = anio });

        var nombre = await conn.ExecuteScalarAsync<string>(@"
            SELECT Nombres + ' ' + Apellidos FROM Rrhh.Empleados WHERE EmpleadoID = @EmpleadoID",
            new { EmpleadoID = empleadoId }) ?? "Empleado";

        var comisionBase  = ventas.TotalVentas * config.PorcentajeBase / 100m;
        var bono          = ventas.TotalVentas > 0 && config.MetaMensual > 0 && ventas.TotalVentas >= config.MetaMensual
                                ? config.BonoPorMeta : 0m;
        var totalComision = comisionBase + bono;
        var pctMeta       = config.MetaMensual > 0
            ? (double)(ventas.TotalVentas / config.MetaMensual * 100m)
            : 100d;

        return new DashboardComisionItem(
            empleadoId, nombre,
            config.MetaMensual, config.PorcentajeBase, config.BonoPorMeta,
            ventas.TotalVentas, comisionBase, bono, totalComision,
            ventas.TotalFacturas, pctMeta);
    }

    private record ConfigRow(decimal PorcentajeBase, decimal MetaMensual, decimal BonoPorMeta);
    private record VentasRow(decimal TotalVentas, int TotalFacturas);

    public async Task<IEnumerable<ResumenComisionesItem>> ResumenTodosAsync(int mes, int anio)
    {
        using var conn = db.CreateConnection();
        return await conn.QueryAsync<ResumenComisionesItem>(@"
            SELECT
                e.EmpleadoID,
                e.Nombres + ' ' + e.Apellidos AS NombreEmpleado,
                ISNULL(c.MetaMensual, 0) AS MetaMensual,
                ISNULL(SUM(l.Cantidad * l.PrecioUnitario), 0) AS TotalVentas,
                CAST(ISNULL(SUM(l.Cantidad * l.PrecioUnitario), 0) * ISNULL(c.PorcentajeBase, 0) / 100 AS DECIMAL(18,4))
                    + CASE WHEN ISNULL(c.MetaMensual, 0) > 0
                                AND ISNULL(SUM(l.Cantidad * l.PrecioUnitario), 0) > 0
                                AND ISNULL(SUM(l.Cantidad * l.PrecioUnitario), 0) >= ISNULL(c.MetaMensual, 0)
                           THEN ISNULL(c.BonoPorMeta, 0) ELSE CAST(0 AS DECIMAL(18,4)) END AS TotalComision,
                CAST(CASE WHEN ISNULL(c.MetaMensual, 0) = 0 THEN 100.0
                          ELSE ISNULL(SUM(l.Cantidad * l.PrecioUnitario), 0) / c.MetaMensual * 100.0
                     END AS FLOAT) AS PorcentajeMeta,
                COUNT(DISTINCT f.FacturaID) AS TotalFacturas
            FROM Rrhh.ConfigComision c
            JOIN Rrhh.Empleados e ON e.EmpleadoID = c.EmpleadoID
            LEFT JOIN Facturacion.Facturas f
                ON f.VendedorID = c.EmpleadoID
               AND MONTH(f.Fecha) = @Mes
               AND YEAR(f.Fecha)  = @Anio
            LEFT JOIN Facturacion.FacturaLineas l ON l.FacturaID = f.FacturaID
            WHERE c.Activo = 1
            GROUP BY e.EmpleadoID, e.Nombres, e.Apellidos,
                     c.MetaMensual, c.PorcentajeBase, c.BonoPorMeta
            ORDER BY TotalVentas DESC",
            new { Mes = mes, Anio = anio });
    }
}
