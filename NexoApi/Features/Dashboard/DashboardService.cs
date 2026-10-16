using Dapper;
using Microsoft.Extensions.Caching.Memory;
using NexoApi.Common.Data;
using NexoApi.Features.Dashboard.Dtos;

namespace NexoApi.Features.Dashboard;

public interface IDashboardService
{
    Task<IEnumerable<PlanVsRealPunto>> ObtenerPlanVsRealAsync(DateTime desde, DateTime hasta);
    Task<IEnumerable<DistribucionCentroCostoItem>> ObtenerDistribucionCentroCostoAsync();
    Task<IEnumerable<PerdidaPorMotivoItem>> ObtenerPerdidasPorMotivoAsync(DateTime desde, DateTime hasta);
    Task<IEnumerable<TendenciaCostoPunto>> ObtenerTendenciaCostoAsync(int articuloId);
    Task<IEnumerable<CumplimientoCentroCostoItem>> ObtenerCumplimientoAsync();

    // Resumenes transversales para BI (agosto 2026) -- cada uno lee de su
    // propio modulo, no hay tabla de agregacion propia del Dashboard.
    Task<ResumenCrmItem> ObtenerResumenCrmAsync(DateTime desde, DateTime hasta);
    Task<IEnumerable<EmpleadosPorCentroCostoItem>> ObtenerEmpleadosPorCentroCostoAsync();
    Task<ResumenPlanificacionItem> ObtenerResumenPlanificacionAsync();
    Task<ResumenInventarioItem> ObtenerResumenInventarioAsync();
    Task<IEnumerable<VentasPorDepartamentoItem>> ObtenerVentasPorDepartamentoAsync();

    // Tab Facturacion BI
    Task<ResumenFacturacionItem> ObtenerResumenFacturacionAsync(DateTime desde, DateTime hasta);
    Task<IEnumerable<IngresoPorMesPunto>> ObtenerIngresosPorMesAsync(int meses);
    Task<IEnumerable<TopClienteItem>> ObtenerTopClientesAsync(DateTime desde, DateTime hasta, int top = 10);
    Task<IEnumerable<TopArticuloItem>> ObtenerTopArticulosAsync(DateTime desde, DateTime hasta, int top = 10);

    // Tab Financiero BI
    Task<IEnumerable<MargenArticuloItem>> ObtenerMargenPorArticuloAsync(DateTime desde, DateTime hasta, int top = 20);
    Task<IEnumerable<AlertaStockItem>> ObtenerAlertasStockAsync();
    Task<IEnumerable<ComparativaMesItem>> ObtenerComparativaYoYAsync(int meses);

    // Feed de actividad y sparklines
    Task<IEnumerable<ActividadItem>> ObtenerActividadRecienteAsync(int n = 20);
    Task<SparklinesDashboard> ObtenerSparklinesAsync();

    Task<ResumenMaquinariaItem> ObtenerResumenMaquinariaAsync();

    // Inteligencia de ventas
    Task<IEnumerable<HorarioPicoItem>> ObtenerHorariosPicoAsync(int meses = 3);
    Task<IEnumerable<ParComplementarioItem>> ObtenerParesComplementariosAsync(int top = 10);
}

public class DashboardService : IDashboardService
{
    private readonly IDbConnectionFactory _db;
    private readonly IMemoryCache _cache;

    public DashboardService(IDbConnectionFactory db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IEnumerable<PlanVsRealPunto>> ObtenerPlanVsRealAsync(DateTime desde, DateTime hasta)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT Fecha, SUM(TotalPlanificado) AS TotalPlanificado, SUM(TotalReal) AS TotalReal
            FROM Produccion.vw_ProduccionPlanVsReal
            WHERE Fecha >= @Desde AND Fecha <= @Hasta
            GROUP BY Fecha
            ORDER BY Fecha";

        return await connection.QueryAsync<PlanVsRealPunto>(sql, new { Desde = desde.Date, Hasta = hasta.Date });
    }

    public async Task<IEnumerable<DistribucionCentroCostoItem>> ObtenerDistribucionCentroCostoAsync()
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT CentroCostoID, CentroCosto, TotalOrdenes, TotalUnidadesProducidas, InversionTotal
            FROM Produccion.vw_DistribucionPorCentroCosto
            ORDER BY TotalUnidadesProducidas DESC";

        return await connection.QueryAsync<DistribucionCentroCostoItem>(sql);
    }

    public async Task<IEnumerable<PerdidaPorMotivoItem>> ObtenerPerdidasPorMotivoAsync(DateTime desde, DateTime hasta)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT Motivo, SUM(CantidadTotalPerdida) AS CantidadTotalPerdida, SUM(ValorTotalPerdido) AS ValorTotalPerdido
            FROM Kardex.vw_PerdidasPorMotivo
            WHERE Fecha >= @Desde AND Fecha <= @Hasta
            GROUP BY Motivo
            ORDER BY ValorTotalPerdido DESC";

        return await connection.QueryAsync<PerdidaPorMotivoItem>(sql, new { Desde = desde.Date, Hasta = hasta.Date });
    }

    public async Task<IEnumerable<TendenciaCostoPunto>> ObtenerTendenciaCostoAsync(int articuloId)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT Fecha, CostoUnitarioReal
            FROM Produccion.vw_TendenciaCostoUnitario
            WHERE ArticuloID = @ArticuloId
            ORDER BY Fecha";

        return await connection.QueryAsync<TendenciaCostoPunto>(sql, new { ArticuloId = articuloId });
    }

    public async Task<IEnumerable<CumplimientoCentroCostoItem>> ObtenerCumplimientoAsync()
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT CentroCostoID, CentroCosto, TotalOrdenesFinalizadas, OrdenesATiempo,
                   ISNULL(PorcentajeCumplimiento, 0) AS PorcentajeCumplimiento
            FROM Produccion.vw_CumplimientoPlanificacion
            ORDER BY PorcentajeCumplimiento DESC";
        return await connection.QueryAsync<CumplimientoCentroCostoItem>(sql);
    }

    public async Task<ResumenCrmItem> ObtenerResumenCrmAsync(DateTime desde, DateTime hasta)
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT
                (SELECT COUNT(*) FROM Crm.Clientes    WHERE FechaCreacion >= @Desde AND FechaCreacion <= @Hasta) AS ClientesNuevos,
                (SELECT COUNT(*) FROM Crm.Interacciones WHERE Fecha >= @Desde AND Fecha <= @Hasta)               AS Interacciones,
                (SELECT COUNT(*) FROM Crm.Cotizaciones WHERE Fecha >= @Desde AND Fecha <= @Hasta)                AS CotizacionesTotal,
                (SELECT COUNT(*) FROM Crm.Cotizaciones WHERE Fecha >= @Desde AND Fecha <= @Hasta
                                                        AND Estado = 'CONVERTIDA')                               AS Convertidas";

        return await connection.QuerySingleAsync<ResumenCrmItem>(sql, new { Desde = desde.Date, Hasta = hasta.Date });
    }

    public async Task<IEnumerable<EmpleadosPorCentroCostoItem>> ObtenerEmpleadosPorCentroCostoAsync()
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT ISNULL(cc.Nombre, 'Sin asignar') AS CentroCosto, COUNT(*) AS TotalEmpleados
            FROM Rrhh.Empleados e
            LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = e.CentroCostoID
            WHERE e.Estado = 1
            GROUP BY cc.Nombre
            ORDER BY TotalEmpleados DESC";

        return await connection.QueryAsync<EmpleadosPorCentroCostoItem>(sql);
    }

    // Promedio de cumplimiento del mes actual (no historico completo) -- es
    // la foto "de ahora" que tiene sentido en un landing de BI.
    public async Task<ResumenPlanificacionItem> ObtenerResumenPlanificacionAsync()
    {
        using var connection = _db.CreateConnection();

        // NOTA: AVG() no puede envolver directamente una expresion que
        // contiene una subconsulta correlacionada con su propio agregado
        // (SQL Server error 130 "Cannot perform an aggregate function on an
        // expression containing an aggregate or a subquery") -- se resuelve
        // materializando el porcentaje por fila en una tabla derivada y
        // promediando DESPUES, en la consulta externa.
        const string sql = @"
            DECLARE @PeriodoActual DATE = DATEFROMPARTS(YEAR(GETUTCDATE()), MONTH(GETUTCDATE()), 1);

            SELECT
                ISNULL((SELECT AVG(Cumplimiento) FROM (
                    SELECT CASE WHEN d.CantidadProyectada > 0
                         THEN (ISNULL((
                             SELECT SUM(km.Cantidad) FROM Kardex.KardexMovimientos km
                             JOIN Kardex.TiposMovimientoKardex t ON t.TipoMovID = km.TipoMovID
                             WHERE t.Codigo = 'ENTRADA_PT' AND km.ArticuloID = d.ArticuloID
                               AND km.CentroCostoID = d.CentroCostoID AND km.Fecha >= d.Periodo AND km.Fecha < DATEADD(MONTH, 1, d.Periodo)
                         ), 0) / d.CantidadProyectada * 100) ELSE 0 END AS Cumplimiento
                    FROM Planificacion.DemandaProyectada d WHERE d.Periodo = @PeriodoActual
                ) x), 0) AS CumplimientoDemandaPromedio,
                ISNULL((SELECT AVG(Cumplimiento) FROM (
                    SELECT CASE WHEN m.MetaValor > 0
                         THEN (ISNULL((
                             SELECT SUM(ABS(km.Cantidad) * a.PPublico) FROM Kardex.KardexMovimientos km
                             JOIN Kardex.TiposMovimientoKardex t ON t.TipoMovID = km.TipoMovID
                             JOIN Catalogo.Tarjetas a ON a.ArticuloID = km.ArticuloID
                             JOIN Inventario.Bodegas b ON b.BodegaID = km.BodegaID
                             WHERE t.Codigo IN ('SALIDA_VENTA_VISIONS', 'SALIDA_VENTA_FACTURA') AND b.CentroCostoID = m.CentroCostoID
                               AND km.Fecha >= m.Periodo AND km.Fecha < DATEADD(MONTH, 1, m.Periodo)
                         ), 0) / m.MetaValor * 100) ELSE 0 END AS Cumplimiento
                    FROM Planificacion.MetasVenta m WHERE m.Periodo = @PeriodoActual
                ) y), 0) AS CumplimientoVentaPromedio";

        return await connection.QuerySingleAsync<ResumenPlanificacionItem>(sql);
    }

    public async Task<ResumenInventarioItem> ObtenerResumenInventarioAsync()
    {
        using var connection = _db.CreateConnection();

        const string sql = @"
            SELECT ISNULL(SUM(ValorTotal), 0) AS ValorTotalStock,
                   ISNULL(SUM(CASE WHEN RequierePedido = 1 THEN 1 ELSE 0 END), 0) AS ArticulosConAlerta
            FROM Inventario.vw_StockConsolidado";

        return await connection.QuerySingleAsync<ResumenInventarioItem>(sql);
    }

    public async Task<ResumenFacturacionItem> ObtenerResumenFacturacionAsync(DateTime desde, DateTime hasta)
    {
        using var connection = _db.CreateConnection();
        // Une facturas creadas en NEXO (excluye importadas de Visions para no duplicar)
        // con ventas de Visions leídas directo de EventosEntrantes (fuente de verdad para precios Visions).
        const string sql = @"
            SELECT
                ISNULL(SUM(src.TotalEmitido), 0)    AS TotalEmitido,
                ISNULL(SUM(src.TotalCobrado), 0)    AS TotalCobrado,
                ISNULL(SUM(src.SaldoPendiente), 0)  AS SaldoPendiente,
                ISNULL(SUM(src.Docs), 0)            AS DocumentosEmitidos
            FROM (
                SELECT
                    ISNULL(tot.Total, 0) AS TotalEmitido,
                    ISNULL(pag.TotalPagado, 0) AS TotalCobrado,
                    ISNULL(tot.Total, 0) - ISNULL(pag.TotalPagado, 0) AS SaldoPendiente,
                    1 AS Docs
                FROM Facturacion.Facturas f
                LEFT JOIN (SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                           FROM Facturacion.FacturaLineas GROUP BY FacturaID) tot ON tot.FacturaID = f.FacturaID
                LEFT JOIN (SELECT FacturaID, SUM(Monto) AS TotalPagado
                           FROM Facturacion.Pagos GROUP BY FacturaID) pag ON pag.FacturaID = f.FacturaID
                WHERE f.VisionsConfirmado = 0
                  AND f.Fecha >= @Desde AND f.Fecha <= @Hasta

                UNION ALL

                SELECT
                    SUM(ISNULL(ee.Cantidad, 0) * ISNULL(ee.PrecioArticuloVisions, 0)) AS TotalEmitido,
                    SUM(ISNULL(ee.Cantidad, 0) * ISNULL(ee.PrecioArticuloVisions, 0)) AS TotalCobrado,
                    0 AS SaldoPendiente,
                    1 AS Docs
                FROM Integracion.EventosEntrantes ee
                WHERE ee.TipDoc IS NOT NULL
                  AND CAST(ee.FechaEventoOrigen AS DATE) >= @Desde
                  AND CAST(ee.FechaEventoOrigen AS DATE) <= @Hasta
                GROUP BY ee.TipDoc, ee.NroDoc, ee.CentroCostoID
            ) src";
        return await connection.QuerySingleAsync<ResumenFacturacionItem>(sql, new { Desde = desde.Date, Hasta = hasta.Date });
    }

    public async Task<IEnumerable<IngresoPorMesPunto>> ObtenerIngresosPorMesAsync(int meses)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT Anio, Mes, MAX(NombreMes) AS NombreMes,
                   SUM(TotalFacturado) AS TotalFacturado,
                   SUM(TotalCobrado)   AS TotalCobrado
            FROM (
                -- NEXO (excluye confirmados Visions para no duplicar)
                SELECT YEAR(f.Fecha) AS Anio, MONTH(f.Fecha) AS Mes,
                       DATENAME(MONTH, f.Fecha) AS NombreMes,
                       ISNULL(SUM(ISNULL(tot.Total,0)), 0)       AS TotalFacturado,
                       ISNULL(SUM(ISNULL(pag.TotalPagado,0)), 0) AS TotalCobrado
                FROM Facturacion.Facturas f
                LEFT JOIN (SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                           FROM Facturacion.FacturaLineas GROUP BY FacturaID) tot ON tot.FacturaID = f.FacturaID
                LEFT JOIN (SELECT FacturaID, SUM(Monto) AS TotalPagado
                           FROM Facturacion.Pagos GROUP BY FacturaID) pag ON pag.FacturaID = f.FacturaID
                WHERE f.VisionsConfirmado = 0
                  AND f.Fecha >= DATEADD(MONTH, -@Meses, CAST(GETUTCDATE() AS DATE))
                GROUP BY YEAR(f.Fecha), MONTH(f.Fecha), DATENAME(MONTH, f.Fecha)

                UNION ALL

                -- Visions EventosEntrantes
                SELECT YEAR(CAST(ee.FechaEventoOrigen AS DATE)) AS Anio,
                       MONTH(CAST(ee.FechaEventoOrigen AS DATE)) AS Mes,
                       DATENAME(MONTH, ee.FechaEventoOrigen) AS NombreMes,
                       SUM(ISNULL(ee.Cantidad,0) * ISNULL(ee.PrecioArticuloVisions,0)) AS TotalFacturado,
                       SUM(ISNULL(ee.Cantidad,0) * ISNULL(ee.PrecioArticuloVisions,0)) AS TotalCobrado
                FROM Integracion.EventosEntrantes ee
                WHERE ee.TipDoc IS NOT NULL
                  AND CAST(ee.FechaEventoOrigen AS DATE) >= DATEADD(MONTH, -@Meses, CAST(GETUTCDATE() AS DATE))
                GROUP BY YEAR(CAST(ee.FechaEventoOrigen AS DATE)),
                         MONTH(CAST(ee.FechaEventoOrigen AS DATE)),
                         DATENAME(MONTH, ee.FechaEventoOrigen)
            ) src
            GROUP BY Anio, Mes
            ORDER BY Anio, Mes";
        return await connection.QueryAsync<IngresoPorMesPunto>(sql, new { Meses = meses });
    }

    public async Task<IEnumerable<TopClienteItem>> ObtenerTopClientesAsync(DateTime desde, DateTime hasta, int top = 10)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT TOP (@Top)
                ISNULL(MAX(c.ClienteID), 0)                AS ClienteID,
                MAX(ISNULL(c.Nombre, src.NombreCliente))   AS Cliente,
                src.NIT,
                SUM(src.Importe)                           AS TotalFacturado,
                SUM(src.Docs)                              AS NumDocumentos
            FROM (
                -- NEXO (excluye confirmados Visions)
                SELECT ISNULL(c2.NIT, 'SIN-NIT')    AS NIT,
                       ISNULL(c2.Nombre, 'Sin cliente') AS NombreCliente,
                       ISNULL(tot.Total, 0)             AS Importe,
                       1                                AS Docs
                FROM Facturacion.Facturas f
                LEFT JOIN Crm.Clientes c2 ON c2.ClienteID = f.ClienteID
                LEFT JOIN (SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                           FROM Facturacion.FacturaLineas GROUP BY FacturaID) tot ON tot.FacturaID = f.FacturaID
                WHERE f.VisionsConfirmado = 0 AND f.Fecha >= @Desde AND f.Fecha <= @Hasta

                UNION ALL

                -- Visions EventosEntrantes
                SELECT ISNULL(ee.NitCliente, 'SIN-NIT')     AS NIT,
                       ISNULL(ee.NombreCliente, 'Sin nombre') AS NombreCliente,
                       ISNULL(ee.Cantidad, 0) * ISNULL(ee.PrecioArticuloVisions, 0) AS Importe,
                       1 AS Docs
                FROM Integracion.EventosEntrantes ee
                WHERE ee.TipDoc IS NOT NULL
                  AND CAST(ee.FechaEventoOrigen AS DATE) >= @Desde
                  AND CAST(ee.FechaEventoOrigen AS DATE) <= @Hasta
            ) src
            LEFT JOIN Crm.Clientes c ON c.NIT = src.NIT
            GROUP BY src.NIT
            ORDER BY TotalFacturado DESC";
        return await connection.QueryAsync<TopClienteItem>(sql, new { Top = top, Desde = desde.Date, Hasta = hasta.Date });
    }

    public async Task<IEnumerable<TopArticuloItem>> ObtenerTopArticulosAsync(DateTime desde, DateTime hasta, int top = 10)
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT TOP (@Top)
                ISNULL(MAX(a2.ArticuloID), 0)       AS ArticuloID,
                src.SKU,
                MAX(ISNULL(a2.Nombre, src.Nombre))  AS Nombre,
                SUM(src.Cantidad)                   AS CantidadVendida,
                SUM(src.Importe)                    AS TotalFacturado
            FROM (
                -- NEXO (excluye confirmados Visions)
                SELECT a.Referencia AS SKU, a.Nombre,
                       ABS(fl.Cantidad)                     AS Cantidad,
                       ABS(fl.Cantidad) * fl.PrecioUnitario AS Importe
                FROM Facturacion.FacturaLineas fl
                JOIN Facturacion.Facturas f ON f.FacturaID  = fl.FacturaID
                JOIN Catalogo.Tarjetas a    ON a.ArticuloID = fl.ArticuloID
                WHERE f.VisionsConfirmado = 0 AND f.Fecha >= @Desde AND f.Fecha <= @Hasta

                UNION ALL

                -- Visions EventosEntrantes
                SELECT ee.CodigoArticuloVisions AS SKU,
                       ISNULL(ee.NombreArticuloVisions, ee.CodigoArticuloVisions) AS Nombre,
                       ABS(ISNULL(ee.Cantidad, 0))                                               AS Cantidad,
                       ABS(ISNULL(ee.Cantidad, 0)) * ISNULL(ee.PrecioArticuloVisions, 0)         AS Importe
                FROM Integracion.EventosEntrantes ee
                WHERE ee.TipDoc IS NOT NULL
                  AND CAST(ee.FechaEventoOrigen AS DATE) >= @Desde
                  AND CAST(ee.FechaEventoOrigen AS DATE) <= @Hasta
            ) src
            LEFT JOIN Catalogo.Tarjetas a2 ON a2.Referencia = src.SKU
            GROUP BY src.SKU
            ORDER BY TotalFacturado DESC";
        return await connection.QueryAsync<TopArticuloItem>(sql, new { Top = top, Desde = desde.Date, Hasta = hasta.Date });
    }

    public async Task<IEnumerable<MargenArticuloItem>> ObtenerMargenPorArticuloAsync(DateTime desde, DateTime hasta, int top = 20)
    {
        var cacheKey = $"margen_{desde:yyyyMMdd}_{hasta:yyyyMMdd}_{top}";
        if (_cache.TryGetValue(cacheKey, out IEnumerable<MargenArticuloItem>? cached) && cached is not null)
            return cached;

        using var connection = _db.CreateConnection();
        // Costo unitario por artículo en orden de prioridad:
        // 1) CostoPromedio (actualizado por Kardex si hay movimientos de inventario)
        // 2) Promedio de CostoUnitarioReal de órdenes de producción cerradas
        // 3) Costo calculado desde receta BOM (suma insumos × Costo del insumo)
        // 4) Campo Costo ingresado manualmente en el catálogo
        const string sql = @"
            WITH CostosOP AS (
                SELECT ProductoTerminadoID AS ArticuloID,
                       AVG(CostoUnitarioReal) AS CostoOP
                FROM Produccion.OrdenesProduccion
                WHERE CostoUnitarioReal > 0
                GROUP BY ProductoTerminadoID
            ),
            CostosBOM AS (
                SELECT rb.ProductoTerminadoID AS ArticuloID,
                       SUM(rd.CantidadRequerida * ISNULL(ti.Costo, 0)) AS CostoBOM
                FROM Produccion.RecetaBOM rb
                JOIN Produccion.RecetaBOM_Detalle rd ON rd.RecetaID = rb.RecetaID
                JOIN Catalogo.Tarjetas ti             ON ti.ArticuloID = rd.InsumoID
                WHERE rb.Estado = 1
                GROUP BY rb.ProductoTerminadoID
            ),
            Ventas AS (
                SELECT fl.ArticuloID,
                       SUM(ABS(fl.Cantidad))                    AS CantidadVendida,
                       SUM(ABS(fl.Cantidad) * fl.PrecioUnitario) AS TotalFacturado
                FROM Facturacion.FacturaLineas fl
                JOIN Facturacion.Facturas f ON f.FacturaID = fl.FacturaID
                WHERE f.Fecha >= @Desde AND f.Fecha <= @Hasta
                GROUP BY fl.ArticuloID
            )
            SELECT TOP (@Top)
                a.ArticuloID,
                a.Referencia AS SKU,
                a.Nombre,
                ISNULL(v.CantidadVendida, 0)  AS CantidadVendida,
                ISNULL(v.TotalFacturado, 0)    AS TotalFacturado,
                ISNULL(v.CantidadVendida, 0) *
                    COALESCE(NULLIF(a.CostoPromedio,0), cop.CostoOP, cbom.CostoBOM, NULLIF(a.Costo,0), 0)
                    AS CostoEstimado,
                ISNULL(v.TotalFacturado, 0) -
                    ISNULL(v.CantidadVendida, 0) *
                    COALESCE(NULLIF(a.CostoPromedio,0), cop.CostoOP, cbom.CostoBOM, NULLIF(a.Costo,0), 0)
                    AS MargenBruto,
                CASE WHEN ISNULL(v.TotalFacturado, 0) > 0
                     THEN (ISNULL(v.TotalFacturado, 0) -
                           ISNULL(v.CantidadVendida, 0) *
                           COALESCE(NULLIF(a.CostoPromedio,0), cop.CostoOP, cbom.CostoBOM, NULLIF(a.Costo,0), 0))
                          / v.TotalFacturado * 100
                     ELSE 0 END AS PorcentajeMargen
            FROM Ventas v
            JOIN Catalogo.Tarjetas a  ON a.ArticuloID = v.ArticuloID
            LEFT JOIN CostosOP  cop   ON cop.ArticuloID  = a.ArticuloID
            LEFT JOIN CostosBOM cbom  ON cbom.ArticuloID = a.ArticuloID
            ORDER BY MargenBruto DESC";

        var result = (await connection.QueryAsync<MargenArticuloItem>(sql,
            new { Top = top, Desde = desde.Date, Hasta = hasta.Date })).ToList();

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    public async Task<IEnumerable<AlertaStockItem>> ObtenerAlertasStockAsync()
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT s.ArticuloID, s.SKU, s.Articulo, s.CentroCosto, s.Bodega,
                   s.CantidadActual, ISNULL(a.StockMinimo, 0) AS StockMinimo,
                   ISNULL(a.StockMinimo, 0) - s.CantidadActual AS Deficit
            FROM Inventario.vw_StockConsolidado s
            JOIN Catalogo.Tarjetas a ON a.ArticuloID = s.ArticuloID
            WHERE s.RequierePedido = 1
            ORDER BY Deficit DESC";
        return await connection.QueryAsync<AlertaStockItem>(sql);
    }

    public async Task<IEnumerable<ComparativaMesItem>> ObtenerComparativaYoYAsync(int meses)
    {
        using var con = _db.CreateConnection();
        var desde = new DateTime(NexoApi.Common.ColombiaClock.Today.Year - 1, NexoApi.Common.ColombiaClock.Today.Month, 1).AddMonths(-meses + 1);
        const string sql = @"
            WITH Base AS (
                SELECT YEAR(f.Fecha) AS Anio, MONTH(f.Fecha) AS Mes,
                       ISNULL(SUM(ISNULL(fl.Total,0)), 0) AS TotalFacturado
                FROM Facturacion.Facturas f
                LEFT JOIN (SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                           FROM Facturacion.FacturaLineas GROUP BY FacturaID) fl ON fl.FacturaID = f.FacturaID
                WHERE f.Fecha >= @Desde
                GROUP BY YEAR(f.Fecha), MONTH(f.Fecha)
            )
            SELECT a.Anio, a.Mes,
                DATENAME(month, DATEFROMPARTS(a.Anio, a.Mes, 1)) AS NombreMes,
                ISNULL(a.TotalFacturado, 0) AS TotalActual,
                ISNULL(b.TotalFacturado, 0) AS TotalAnterior,
                CASE WHEN ISNULL(b.TotalFacturado, 0) = 0 THEN 0
                     ELSE ((a.TotalFacturado - b.TotalFacturado) / b.TotalFacturado) * 100
                END AS VariacionPct
            FROM Base a
            LEFT JOIN Base b ON b.Anio = a.Anio - 1 AND b.Mes = a.Mes
            WHERE a.Anio = YEAR(GETDATE())
            ORDER BY a.Mes";
        return await con.QueryAsync<ComparativaMesItem>(sql, new { Desde = desde });
    }

    public async Task<IEnumerable<ActividadItem>> ObtenerActividadRecienteAsync(int n = 20)
    {
        using var con = _db.CreateConnection();
        const string sql = @"
            SELECT TOP (@N) Tipo, Descripcion, Entidad, FechaHora, Link, Icono, Color
            FROM (
                SELECT TOP 7
                    'FACTURA' AS Tipo,
                    'Factura #' + ISNULL(f.NroDoc, CAST(f.FacturaID AS NVARCHAR)) + ' — ' + ISNULL(cl.Nombre, 'Cliente') AS Descripcion,
                    ISNULL(cl.Nombre, 'Cliente') AS Entidad,
                    CAST(f.Fecha AS DATETIME2) AS FechaHora,
                    '/ventas/facturas' AS Link,
                    'receipt' AS Icono,
                    'primary' AS Color
                FROM Facturacion.Facturas f
                LEFT JOIN Crm.Clientes cl ON cl.ClienteID = f.ClienteID
                ORDER BY f.Fecha DESC, f.FacturaID DESC

                UNION ALL

                SELECT TOP 5
                    'OC',
                    'OC ' + oc.Codigo + ' — ' + p.RazonSocial,
                    p.RazonSocial,
                    CAST(oc.FechaEmision AS DATETIME2),
                    '/compras/ordenes',
                    'shopping_cart',
                    'secondary'
                FROM Compras.OrdenesCompra oc
                JOIN Catalogo.Proveedores p ON p.ProveedorID = oc.ProveedorID
                ORDER BY oc.FechaEmision DESC, oc.OrdenCompraID DESC

                UNION ALL

                SELECT TOP 5
                    'PRODUCCION',
                    'OP #' + CAST(op.OrdenProduccionID AS NVARCHAR) + ' — ' + ISNULL(tp.Nombre, '') AS Descripcion,
                    ISNULL(tp.Nombre, 'Producción'),
                    ISNULL(CAST(op.FechaFin AS DATETIME2), CAST(op.FechaCreacion AS DATETIME2)),
                    '/produccion/ordenes',
                    'factory',
                    'success'
                FROM Produccion.OrdenesProduccion op
                LEFT JOIN Produccion.TiposProduccion tp ON tp.TipoProduccionID = op.TipoProduccionID
                WHERE op.FechaFin IS NOT NULL
                ORDER BY op.FechaFin DESC, op.OrdenProduccionID DESC

                UNION ALL

                SELECT TOP 5
                    'COTIZACION',
                    'Cotización #' + CAST(c.CotizacionID AS NVARCHAR) + ' — ' + cl.Nombre,
                    cl.Nombre,
                    CAST(c.Fecha AS DATETIME2),
                    '/crm/cotizaciones',
                    'description',
                    'info'
                FROM Crm.Cotizaciones c
                JOIN Crm.Clientes cl ON cl.ClienteID = c.ClienteID
                WHERE c.Estado IN ('ENVIADA','ACEPTADA')
                ORDER BY c.Fecha DESC, c.CotizacionID DESC

                UNION ALL

                SELECT TOP 3
                    'SYNC',
                    'Visions: ' + CAST(COUNT(*) AS NVARCHAR) + ' eventos confirmados',
                    'Sincronización',
                    CAST(MAX(FechaEnvio) AS DATETIME2),
                    '/admin/integracion-visions',
                    'sync',
                    'default'
                FROM Integracion.EventosSalientes
                WHERE Estado = 'CONFIRMADO'
                  AND FechaEnvio >= DATEADD(day, -7, GETDATE())
                GROUP BY CAST(FechaEnvio AS DATE)
                ORDER BY CAST(FechaEnvio AS DATE) DESC
            ) t
            ORDER BY FechaHora DESC";
        return await con.QueryAsync<ActividadItem>(sql, new { N = n });
    }

    public async Task<SparklinesDashboard> ObtenerSparklinesAsync()
    {
        using var con = _db.CreateConnection();

        var facturas = await con.QueryAsync<SparklineItem>(@"
            SELECT CAST(Fecha AS DATE) AS Fecha, COUNT(*) AS Valor
            FROM Facturacion.Facturas
            WHERE Fecha >= DATEADD(day, -6, CAST(GETDATE() AS DATE))
            GROUP BY CAST(Fecha AS DATE)
            ORDER BY CAST(Fecha AS DATE)");

        var ordenes = await con.QueryAsync<SparklineItem>(@"
            SELECT CAST(FechaCreacion AS DATE) AS Fecha, COUNT(*) AS Valor
            FROM Produccion.OrdenesProduccion
            WHERE FechaCreacion >= DATEADD(day, -6, CAST(GETDATE() AS DATE))
            GROUP BY CAST(FechaCreacion AS DATE)
            ORDER BY CAST(FechaCreacion AS DATE)");

        var cotizaciones = await con.QueryAsync<SparklineItem>(@"
            SELECT CAST(Fecha AS DATE) AS Fecha, COUNT(*) AS Valor
            FROM Crm.Cotizaciones
            WHERE Fecha >= DATEADD(day, -6, CAST(GETDATE() AS DATE))
            GROUP BY CAST(Fecha AS DATE)
            ORDER BY CAST(Fecha AS DATE)");

        return new SparklinesDashboard(facturas, ordenes, cotizaciones);
    }

    public async Task<IEnumerable<VentasPorDepartamentoItem>> ObtenerVentasPorDepartamentoAsync()
    {
        using var connection = _db.CreateConnection();
        const string sql = @"
            SELECT ISNULL(c.Departamento, 'Sin departamento') AS Departamento,
                   ISNULL(SUM(ISNULL(fl.Total,0)), 0)         AS TotalVentas,
                   COUNT(DISTINCT f.ClienteID)                 AS CantidadClientes
            FROM Facturacion.Facturas f
            JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
            LEFT JOIN (SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                       FROM Facturacion.FacturaLineas GROUP BY FacturaID) fl ON fl.FacturaID = f.FacturaID
            WHERE f.Fecha >= DATEADD(month, -12, GETDATE())
            GROUP BY c.Departamento
            ORDER BY TotalVentas DESC";
        return await connection.QueryAsync<VentasPorDepartamentoItem>(sql);
    }

    public async Task<ResumenMaquinariaItem> ObtenerResumenMaquinariaAsync()
    {
        using var connection = _db.CreateConnection();

        var total = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Produccion.Maquinaria WHERE Estado NOT IN ('Inactiva','BajaDefinitiva')");

        var enMant = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Produccion.Maquinaria WHERE Estado = 'EnMantenimiento'");

        var vencido = await connection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*)
            FROM Produccion.Maquinaria m
            WHERE m.Estado NOT IN ('Inactiva','BajaDefinitiva')
              AND (
                  SELECT TOP 1 mm.ProximoMantenimiento
                  FROM Produccion.MantenimientoMaquinaria mm
                  WHERE mm.MaquinariaID = m.MaquinariaID AND mm.ProximoMantenimiento IS NOT NULL
                  ORDER BY mm.FechaRealizado DESC
              ) < CAST(GETDATE() AS DATE)");

        var costoMes = await connection.ExecuteScalarAsync<decimal>(@"
            SELECT ISNULL(SUM(Costo), 0)
            FROM Produccion.MantenimientoMaquinaria
            WHERE YEAR(FechaRealizado) = YEAR(GETDATE())
              AND MONTH(FechaRealizado) = MONTH(GETDATE())");

        return new ResumenMaquinariaItem(total, enMant, vencido, costoMes);
    }

    public async Task<IEnumerable<HorarioPicoItem>> ObtenerHorariosPicoAsync(int meses = 3)
    {
        using var connection = _db.CreateConnection();
        // Combina facturas NEXO + ventas de Visions (EventosEntrantes).
        // En EventosEntrantes cada fila = un ítem; COUNT(DISTINCT IdEventoExterno) = documentos únicos.
        return await connection.QueryAsync<HorarioPicoItem>(@"
            SELECT Hora, DiaSemana, SUM(Total) AS Total
            FROM (
                SELECT DATEPART(HOUR,    FechaCreacion) AS Hora,
                       DATEPART(WEEKDAY, FechaCreacion) AS DiaSemana,
                       COUNT(*) AS Total
                FROM Facturacion.Facturas
                WHERE FechaCreacion >= DATEADD(MONTH, -@Meses, CAST(GETDATE() AS date))
                GROUP BY DATEPART(HOUR, FechaCreacion), DATEPART(WEEKDAY, FechaCreacion)

                UNION ALL

                SELECT DATEPART(HOUR,    CAST(FechaEventoOrigen AS datetime2)) AS Hora,
                       DATEPART(WEEKDAY, CAST(FechaEventoOrigen AS datetime2)) AS DiaSemana,
                       COUNT(DISTINCT IdEventoExterno) AS Total
                FROM Integracion.EventosEntrantes
                WHERE TipDoc IS NOT NULL
                  AND FechaEventoOrigen >= DATEADD(MONTH, -@Meses, CAST(GETDATE() AS date))
                GROUP BY DATEPART(HOUR,    CAST(FechaEventoOrigen AS datetime2)),
                         DATEPART(WEEKDAY, CAST(FechaEventoOrigen AS datetime2))
            ) src
            GROUP BY Hora, DiaSemana
            ORDER BY Hora, DiaSemana",
            new { Meses = meses });
    }

    public async Task<IEnumerable<ParComplementarioItem>> ObtenerParesComplementariosAsync(int top = 10)
    {
        using var connection = _db.CreateConnection();
        return await connection.QueryAsync<ParComplementarioItem>(@"
            SELECT TOP (@Top)
                a.ArticuloID  AS ArticuloAID,
                ta.Nombre     AS ArticuloANombre,
                b.ArticuloID  AS ArticuloBID,
                tb.Nombre     AS ArticuloBNombre,
                COUNT(*)      AS Veces
            FROM Facturacion.FacturaLineas a
            JOIN Facturacion.FacturaLineas b ON a.FacturaID = b.FacturaID AND b.ArticuloID > a.ArticuloID
            JOIN Catalogo.Tarjetas ta ON ta.ArticuloID = a.ArticuloID
            JOIN Catalogo.Tarjetas tb ON tb.ArticuloID = b.ArticuloID
            GROUP BY a.ArticuloID, ta.Nombre, b.ArticuloID, tb.Nombre
            ORDER BY Veces DESC",
            new { Top = top });
    }
}