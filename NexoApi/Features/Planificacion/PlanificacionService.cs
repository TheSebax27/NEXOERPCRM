using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Planificacion.Dtos;

namespace NexoApi.Features.Planificacion;

public interface IPlanificacionService
{
    Task<IEnumerable<DemandaProyectadaItem>> ListarDemandaAsync(int? centroCostoId);
    Task<int> CrearDemandaAsync(CrearDemandaRequest request);
    Task ActualizarDemandaAsync(int demandaId, ActualizarDemandaRequest request);

    Task<IEnumerable<MetaVentaItem>> ListarMetasVentaAsync(int? centroCostoId);
    Task<int> CrearMetaVentaAsync(CrearMetaVentaRequest request);
    Task ActualizarMetaVentaAsync(int metaId, ActualizarMetaVentaRequest request, int usuarioId);

    Task<IEnumerable<DesviacionItem>> ListarDesviacionesAsync(decimal umbral);
    Task<IEnumerable<HistoricoCumplimientoItem>> ObtenerHistoricoCumplimientoAsync(int meses);
    Task<SugerenciaDemandaItem> ObtenerSugerenciaDemandaAsync(int articuloId, int centroCostoId, DateTime periodo);
    Task<IEnumerable<MetaVentaHistorialItem>> ListarHistorialMetaVentaAsync(int metaId);
    Task<IEnumerable<PrediccionDemandaItem>> PrediccionDemandaAsync();
}

public class PlanificacionService : IPlanificacionService
{
    private readonly IDbConnectionFactory _db;

    public PlanificacionService(IDbConnectionFactory db)
    {
        _db = db;
    }

    // CantidadReal: suma de Kardex ENTRADA_PT (produccion terminada real) para
    // el mismo Articulo+CentroCosto+mes calendario que Periodo.
    public async Task<IEnumerable<DemandaProyectadaItem>> ListarDemandaAsync(int? centroCostoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT d.DemandaID, d.ArticuloID, a.Referencia AS SkuArticulo, a.Nombre AS NombreArticulo,
                   d.CentroCostoID, cc.Nombre AS CentroCosto, d.Periodo, d.CantidadProyectada,
                   ISNULL((
                       SELECT SUM(km.Cantidad)
                       FROM Kardex.KardexMovimientos km
                       JOIN Kardex.TiposMovimientoKardex t ON t.TipoMovID = km.TipoMovID
                       WHERE t.Codigo = 'ENTRADA_PT'
                         AND km.ArticuloID = d.ArticuloID
                         AND km.CentroCostoID = d.CentroCostoID
                         AND km.Fecha >= d.Periodo
                         AND km.Fecha < DATEADD(MONTH, 1, d.Periodo)
                   ), 0) AS CantidadReal, d.Notas
            FROM Planificacion.DemandaProyectada d
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = d.ArticuloID
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = d.CentroCostoID
            WHERE (@CentroCostoId IS NULL OR d.CentroCostoID = @CentroCostoId)
            ORDER BY d.Periodo DESC, a.Nombre";

        return await connection.QueryAsync<DemandaProyectadaItem>(sql, new { CentroCostoId = centroCostoId });
    }

    public async Task<int> CrearDemandaAsync(CrearDemandaRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Planificacion.DemandaProyectada (ArticuloID, CentroCostoID, Periodo, CantidadProyectada, Notas)
            OUTPUT INSERTED.DemandaID
            VALUES (@ArticuloID, @CentroCostoID, @Periodo, @CantidadProyectada, @Notas)";

        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    public async Task ActualizarDemandaAsync(int demandaId, ActualizarDemandaRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            UPDATE Planificacion.DemandaProyectada
            SET CantidadProyectada = @CantidadProyectada, Notas = @Notas
            WHERE DemandaID = @DemandaId";

        var filas = await connection.ExecuteAsync(sql, new { DemandaId = demandaId, r.CantidadProyectada, r.Notas });

        if (filas == 0)
            throw new KeyNotFoundException($"No existe la proyeccion de demanda {demandaId}.");
    }

    // VentaReal: Cantidad * PrecioVenta ACTUAL del articulo (aproximacion --
    // no reconstruye el precio historico de cada venta), sumado sobre Kardex
    // SALIDA_VENTA_VISIONS del mismo CentroCosto+mes calendario que Periodo.
    public async Task<IEnumerable<MetaVentaItem>> ListarMetasVentaAsync(int? centroCostoId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT m.MetaID, m.CentroCostoID, cc.Nombre AS CentroCosto, m.Periodo, m.MetaValor,
                   ISNULL((
                       SELECT SUM(ABS(km.Cantidad) * a.PPublico)
                       FROM Kardex.KardexMovimientos km
                       JOIN Kardex.TiposMovimientoKardex t ON t.TipoMovID = km.TipoMovID
                       JOIN Catalogo.Tarjetas a ON a.ArticuloID = km.ArticuloID
                       JOIN Inventario.Bodegas b ON b.BodegaID = km.BodegaID
                       WHERE t.Codigo IN ('SALIDA_VENTA_VISIONS', 'SALIDA_VENTA_FACTURA')
                         AND b.CentroCostoID = m.CentroCostoID
                         AND km.Fecha >= m.Periodo
                         AND km.Fecha < DATEADD(MONTH, 1, m.Periodo)
                   ), 0) AS VentaReal, m.Notas
            FROM Planificacion.MetasVenta m
            JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = m.CentroCostoID
            WHERE (@CentroCostoId IS NULL OR m.CentroCostoID = @CentroCostoId)
            ORDER BY m.Periodo DESC, cc.Nombre";

        return await connection.QueryAsync<MetaVentaItem>(sql, new { CentroCostoId = centroCostoId });
    }

    public async Task<int> CrearMetaVentaAsync(CrearMetaVentaRequest r)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            INSERT INTO Planificacion.MetasVenta (CentroCostoID, Periodo, MetaValor, Notas)
            OUTPUT INSERTED.MetaID
            VALUES (@CentroCostoID, @Periodo, @MetaValor, @Notas)";

        return await connection.ExecuteScalarAsync<int>(sql, r);
    }

    private record MetaActual(decimal MetaValor, string? Notas);

    // Versionado: antes de sobrescribir, el valor ANTERIOR queda guardado en
    // MetasVentaHistorial -- asi se puede auditar quien cambio una meta y por que.
    public async Task ActualizarMetaVentaAsync(int metaId, ActualizarMetaVentaRequest r, int usuarioId)
    {
        using var connection = _db.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var actual = await connection.QuerySingleOrDefaultAsync<MetaActual>(
                "SELECT MetaValor, Notas FROM Planificacion.MetasVenta WHERE MetaID = @MetaId",
                new { MetaId = metaId }, transaction);

            if (actual is null)
                throw new KeyNotFoundException($"No existe la meta de venta {metaId}.");

            await connection.ExecuteAsync(
                @"INSERT INTO Planificacion.MetasVentaHistorial (MetaID, MetaValorAnterior, NotasAnterior, UsuarioID)
                  VALUES (@MetaId, @MetaValorAnterior, @NotasAnterior, @UsuarioID)",
                new { MetaId = metaId, MetaValorAnterior = actual.MetaValor, NotasAnterior = actual.Notas, UsuarioID = usuarioId },
                transaction);

            const string sql = @"
                UPDATE Planificacion.MetasVenta
                SET MetaValor = @MetaValor, Notas = @Notas
                WHERE MetaID = @MetaId";

            await connection.ExecuteAsync(sql, new { MetaId = metaId, r.MetaValor, r.Notas }, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    // ---------- Alertas de desviación ----------
    // Mismo criterio de calculo que DashboardService.ObtenerResumenPlanificacionAsync
    // pero desglosado POR Centro de Costo (no promediado) y filtrado a los que
    // estan por debajo del umbral -- solo detecta, no envia nada.
    public async Task<IEnumerable<DesviacionItem>> ListarDesviacionesAsync(decimal umbral)
    {
        using var connection = _db.CreateConnection();

        // NOTA: mismo problema y misma solucion que DashboardService.ObtenerResumenPlanificacionAsync
        // -- AVG() no puede envolver una subconsulta correlacionada con su
        // propio agregado (SQL Server error 130), asi que cada OUTER APPLY
        // primero materializa el porcentaje por fila y luego promedia.
        const string sql = @"
            DECLARE @PeriodoActual DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);

            SELECT cc.CentroCostoID, cc.Nombre AS CentroCosto,
                   ISNULL(dem.CumplimientoDemanda, 100) AS CumplimientoDemanda,
                   ISNULL(vta.CumplimientoVenta, 100) AS CumplimientoVenta
            FROM Organizacion.CentrosCosto cc
            OUTER APPLY (
                SELECT AVG(Cumplimiento) AS CumplimientoDemanda FROM (
                    SELECT CASE WHEN d.CantidadProyectada > 0
                         THEN (ISNULL((
                             SELECT SUM(km.Cantidad) FROM Kardex.KardexMovimientos km
                             JOIN Kardex.TiposMovimientoKardex t ON t.TipoMovID = km.TipoMovID
                             WHERE t.Codigo = 'ENTRADA_PT' AND km.ArticuloID = d.ArticuloID
                               AND km.CentroCostoID = d.CentroCostoID AND km.Fecha >= d.Periodo AND km.Fecha < DATEADD(MONTH, 1, d.Periodo)
                         ), 0) / d.CantidadProyectada * 100) END AS Cumplimiento
                    FROM Planificacion.DemandaProyectada d WHERE d.CentroCostoID = cc.CentroCostoID AND d.Periodo = @PeriodoActual
                ) x
            ) dem
            OUTER APPLY (
                SELECT AVG(Cumplimiento) AS CumplimientoVenta FROM (
                    SELECT CASE WHEN m.MetaValor > 0
                         THEN (ISNULL((
                             SELECT SUM(ABS(km.Cantidad) * a.PPublico) FROM Kardex.KardexMovimientos km
                             JOIN Kardex.TiposMovimientoKardex t ON t.TipoMovID = km.TipoMovID
                             JOIN Catalogo.Tarjetas a ON a.ArticuloID = km.ArticuloID
                             JOIN Inventario.Bodegas b ON b.BodegaID = km.BodegaID
                             WHERE t.Codigo IN ('SALIDA_VENTA_VISIONS', 'SALIDA_VENTA_FACTURA') AND b.CentroCostoID = m.CentroCostoID
                               AND km.Fecha >= m.Periodo AND km.Fecha < DATEADD(MONTH, 1, m.Periodo)
                         ), 0) / m.MetaValor * 100) END AS Cumplimiento
                    FROM Planificacion.MetasVenta m WHERE m.CentroCostoID = cc.CentroCostoID AND m.Periodo = @PeriodoActual
                ) y
            ) vta
            WHERE (dem.CumplimientoDemanda IS NOT NULL OR vta.CumplimientoVenta IS NOT NULL)
              AND (ISNULL(dem.CumplimientoDemanda, 100) < @Umbral OR ISNULL(vta.CumplimientoVenta, 100) < @Umbral)
            ORDER BY CumplimientoDemanda";

        return await connection.QueryAsync<DesviacionItem>(sql, new { Umbral = umbral });
    }

    // Dapper no soporta mapear una fila directo a ValueTuple (ver leccion
    // 20.30 en CLAUDE.md) -- se usa un record privado en su lugar.
    private record VentaPorPeriodo(DateTime Periodo, decimal CumplimientoVentaPromedio);

    // ---------- Comparativo histórico multi-mes ----------
    public async Task<IEnumerable<HistoricoCumplimientoItem>> ObtenerHistoricoCumplimientoAsync(int meses)
    {
        using var connection = _db.CreateConnection();

        // NOTA: mismo problema y misma solucion que ListarDesviacionesAsync/
        // DashboardService.ObtenerResumenPlanificacionAsync -- se materializa
        // el porcentaje por fila en una tabla derivada antes de agrupar/promediar.
        const string sql = @"
            DECLARE @Desde DATE = DATEADD(MONTH, -@Meses + 1, DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1));

            SELECT Periodo, AVG(Cumplimiento) AS CumplimientoDemandaPromedio, CAST(0 AS DECIMAL(18,4)) AS CumplimientoVentaPromedio
            FROM (
                SELECT d.Periodo,
                       CASE WHEN d.CantidadProyectada > 0
                           THEN (ISNULL((
                               SELECT SUM(km.Cantidad) FROM Kardex.KardexMovimientos km
                               JOIN Kardex.TiposMovimientoKardex t ON t.TipoMovID = km.TipoMovID
                               WHERE t.Codigo = 'ENTRADA_PT' AND km.ArticuloID = d.ArticuloID
                                 AND km.CentroCostoID = d.CentroCostoID AND km.Fecha >= d.Periodo AND km.Fecha < DATEADD(MONTH, 1, d.Periodo)
                           ), 0) / d.CantidadProyectada * 100) ELSE 0 END AS Cumplimiento
                FROM Planificacion.DemandaProyectada d
                WHERE d.Periodo >= @Desde
            ) x
            GROUP BY Periodo
            ORDER BY Periodo";

        var demanda = (await connection.QueryAsync<HistoricoCumplimientoItem>(sql, new { Meses = meses })).ToList();

        const string sqlVenta = @"
            DECLARE @Desde DATE = DATEADD(MONTH, -@Meses + 1, DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1));

            SELECT Periodo, AVG(Cumplimiento) AS CumplimientoVentaPromedio
            FROM (
                SELECT m.Periodo,
                       CASE WHEN m.MetaValor > 0
                           THEN (ISNULL((
                               SELECT SUM(ABS(km.Cantidad) * a.PPublico) FROM Kardex.KardexMovimientos km
                               JOIN Kardex.TiposMovimientoKardex t ON t.TipoMovID = km.TipoMovID
                               JOIN Catalogo.Tarjetas a ON a.ArticuloID = km.ArticuloID
                               JOIN Inventario.Bodegas b ON b.BodegaID = km.BodegaID
                               WHERE t.Codigo IN ('SALIDA_VENTA_VISIONS', 'SALIDA_VENTA_FACTURA') AND b.CentroCostoID = m.CentroCostoID
                                 AND km.Fecha >= m.Periodo AND km.Fecha < DATEADD(MONTH, 1, m.Periodo)
                           ), 0) / m.MetaValor * 100) ELSE 0 END AS Cumplimiento
                FROM Planificacion.MetasVenta m
                WHERE m.Periodo >= @Desde
            ) y
            GROUP BY Periodo";

        var venta = (await connection.QueryAsync<VentaPorPeriodo>(sqlVenta, new { Meses = meses }))
            .ToDictionary(v => v.Periodo, v => v.CumplimientoVentaPromedio);

        // Se combinan los dos resultados (demanda y venta corren en tablas
        // distintas, cada una con sus propios periodos) en una sola serie por mes.
        var periodos = demanda.Select(d => d.Periodo).Union(venta.Keys).OrderBy(p => p);

        return periodos.Select(p =>
        {
            var d = demanda.FirstOrDefault(x => x.Periodo == p);
            var v = venta.TryGetValue(p, out var vv) ? vv : 0;
            return new HistoricoCumplimientoItem(p, d?.CumplimientoDemandaPromedio ?? 0, v);
        }).ToList();
    }

    // ---------- Proyección sugerida ----------
    public async Task<SugerenciaDemandaItem> ObtenerSugerenciaDemandaAsync(int articuloId, int centroCostoId, DateTime periodo)
    {
        using var connection = _db.CreateConnection();

        // Promedio de lo realmente producido en los 3 meses calendario
        // anteriores al periodo elegido (no incluye el propio periodo).
        const string sql = @"
            SELECT AVG(CantidadMes) FROM (
                SELECT DATEADD(MONTH, -n.Numero, @Periodo) AS Mes,
                       ISNULL((
                           SELECT SUM(km.Cantidad) FROM Kardex.KardexMovimientos km
                           JOIN Kardex.TiposMovimientoKardex t ON t.TipoMovID = km.TipoMovID
                           WHERE t.Codigo = 'ENTRADA_PT' AND km.ArticuloID = @ArticuloID AND km.CentroCostoID = @CentroCostoID
                             AND km.Fecha >= DATEADD(MONTH, -n.Numero, @Periodo) AND km.Fecha < DATEADD(MONTH, -n.Numero + 1, @Periodo)
                       ), 0) AS CantidadMes
                FROM (VALUES (1), (2), (3)) AS n(Numero)
            ) meses";

        var sugerida = await connection.ExecuteScalarAsync<decimal?>(sql, new { ArticuloID = articuloId, CentroCostoID = centroCostoId, Periodo = periodo });

        return new SugerenciaDemandaItem(sugerida ?? 0, 3);
    }

    // ---------- Versionado de metas ----------
    public async Task<IEnumerable<MetaVentaHistorialItem>> ListarHistorialMetaVentaAsync(int metaId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT h.HistorialID, h.MetaID, h.MetaValorAnterior, h.NotasAnterior, h.FechaCambio,
                   CONCAT(u.Nombres, ' ', u.Apellidos) AS Usuario
            FROM Planificacion.MetasVentaHistorial h
            LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = h.UsuarioID
            WHERE h.MetaID = @MetaId
            ORDER BY h.FechaCambio DESC";

        return await connection.QueryAsync<MetaVentaHistorialItem>(sql, new { MetaId = metaId });
    }

    // ---------- Predicción de demanda — media móvil 3 meses ----------
    // Calcula consumo de salidas (Cantidad < 0) de los últimos 3 meses calendario
    // completos para cada artículo. Devuelve solo artículos con al menos una salida.
    public async Task<IEnumerable<PrediccionDemandaItem>> PrediccionDemandaAsync()
    {
        using var connection = _db.CreateConnection();

        // PrimerDiaMesActual calculado en C# para pasarlo como parámetro.
        var hoyMes = new DateTime(NexoApi.Common.ColombiaClock.Today.Year, NexoApi.Common.ColombiaClock.Today.Month, 1);

        const string sql = @"
            WITH Consumo AS (
                SELECT km.ArticuloID,
                       SUM(CASE WHEN km.Fecha >= DATEADD(month, -1, @HoyMes) AND km.Fecha < @HoyMes
                                THEN ABS(km.Cantidad) ELSE 0 END) AS ConsumoM1,
                       SUM(CASE WHEN km.Fecha >= DATEADD(month, -2, @HoyMes) AND km.Fecha < DATEADD(month, -1, @HoyMes)
                                THEN ABS(km.Cantidad) ELSE 0 END) AS ConsumoM2,
                       SUM(CASE WHEN km.Fecha >= DATEADD(month, -3, @HoyMes) AND km.Fecha < DATEADD(month, -2, @HoyMes)
                                THEN ABS(km.Cantidad) ELSE 0 END) AS ConsumoM3
                FROM Kardex.KardexMovimientos km
                WHERE km.Cantidad < 0
                  AND km.Fecha >= DATEADD(month, -3, @HoyMes)
                  AND km.Fecha < @HoyMes
                GROUP BY km.ArticuloID
            )
            SELECT a.ArticuloID,
                   a.Referencia AS SKU,
                   a.Nombre     AS Articulo,
                   ta.Nombre    AS TipoArticulo,
                   c.ConsumoM3,
                   c.ConsumoM2,
                   c.ConsumoM1,
                   CAST((c.ConsumoM1 + c.ConsumoM2 + c.ConsumoM3) / 3.0 AS decimal(18,4)) AS MediaMovil
            FROM Consumo c
            JOIN Catalogo.Tarjetas a        ON a.ArticuloID = c.ArticuloID
            JOIN Catalogo.TiposArticulo ta  ON ta.TipoArticuloID = a.TipoArticuloID
            WHERE c.ConsumoM1 + c.ConsumoM2 + c.ConsumoM3 > 0
            ORDER BY MediaMovil DESC";

        return await connection.QueryAsync<PrediccionDemandaItem>(sql, new { HoyMes = hoyMes });
    }
}
