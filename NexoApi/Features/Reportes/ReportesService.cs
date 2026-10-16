using Dapper;
using NexoApi.Common.Data;
using NexoApi.Common.Export;
using NexoApi.Features.Reportes.Dtos;

namespace NexoApi.Features.Reportes;

public interface IReportesService
{
    IEnumerable<ReporteInfo> ListarReportes();
    Task<IEnumerable<Dictionary<string, object?>>> EjecutarAsync(ReporteRequest req);
    Task<byte[]> ExportarExcelAsync(ReporteRequest req);
    Task<byte[]> ExportarPdfAsync(ReporteRequest req, string empresa);
}

public class ReportesService : IReportesService
{
    private readonly IDbConnectionFactory _db;

    public ReportesService(IDbConnectionFactory db) => _db = db;

    private static readonly ReporteInfo[] _reportes =
    [
        new("stock_actual",            "Stock Actual",
            "Existencias actuales por artículo con estado de stock.", ["SKU","Articulo","Stock","StockMinimo","Estado"]),
        new("inventario_valorizado",   "Inventario Valorizado",
            "Valor total en bodega por artículo: stock × costo promedio.", ["SKU","Articulo","Stock","CostoPromedio","ValorTotal"]),
        new("margen_utilidad",         "Margen de Utilidad",
            "Utilidad bruta y margen porcentual por artículo activo.", ["SKU","Articulo","PrecioVenta","Costo","UtilidadBruta","MargenPct"]),
        new("ventas_por_periodo",      "Ventas por Período",
            "Totales diarios de ventas NEXO y unidades vendidas por Visions POS.", ["Fecha","TotalNEXO","FacturasNEXO","TotalVisions","UnidadesVisions"]),
        new("ventas_por_articulo",     "Ventas por Artículo",
            "Cantidad vendida por canal (NEXO y Visions) y valor total.", ["SKU","Articulo","CantidadNEXO","CantidadVisions","CantidadTotal","TotalFacturado"]),
        new("ventas_por_cliente",      "Ventas por Cliente",
            "Total facturado y documentos por cliente en el período (NEXO + Visions).", ["Cliente","NIT","TotalFacturado","NumDocumentos","UltimaFactura"]),
        new("top_articulos",           "Top Artículos",
            "Artículos más vendidos en el período combinando todos los canales.", ["SKU","Articulo","CantidadTotal","TotalFacturado","CanalPrincipal"]),
        new("articulos_sin_movimiento","Artículos sin Movimiento",
            "Artículos sin kardex desde el inicio del período.", ["SKU","Articulo","Stock","UltimoMovimiento","DiasParado"]),
        new("rotacion_inventario",     "Rotación de Inventario",
            "Velocidad de rotación y días de inventario por artículo.", ["SKU","Articulo","Stock","CantidadVendida","Rotacion","DiasInventario"]),
        new("kardex_individual",       "Kardex Individual",
            "Movimientos de un artículo específico. Ingresa referencia o nombre en el filtro.", ["Fecha","SKU","Articulo","TipoMovimiento","Entrada","Salida","Saldo","CostoUnitario","Observacion","Usuario"]),
        new("rendimiento_insumos",     "Rendimiento de Insumos",
            "Consumo teórico vs real por insumo en órdenes de producción cerradas.", ["OP","Producto","CantidadProgramada","CantidadProducida","Insumo","CantidadTeorica","CantidadReal","Desviacion","DesviacionPct"]),
        new("clientes_todos",          "Todos los Clientes",
            "Lista completa de clientes con tipo y ciudad.", ["Cliente","NIT","Tipo","Ciudad","Responsable","Estado"]),
        new("clientes_nuevos",         "Clientes por Período",
            "Clientes registrados en el período con ciudad y tipo.", ["Cliente","NIT","Tipo","Ciudad","FechaRegistro"]),
        new("tickets_por_estado",      "Tickets por Estado/Prioridad",
            "Conteo de tickets agrupados por estado y prioridad.", ["Estado","Prioridad","Total"]),
        new("oportunidades_embudo",    "Embudo de Ventas",
            "Oportunidades activas por etapa con valor estimado.", ["Etapa","Total","ValorEstimado"]),
        new("nps_resumen",             "NPS por Período",
            "Puntuaciones de satisfacción promedio y distribución.", ["Mes","Anio","Promedio","Total","Promotores","Detractores"]),
    ];

    public IEnumerable<ReporteInfo> ListarReportes() => _reportes;

    public async Task<IEnumerable<Dictionary<string, object?>>> EjecutarAsync(ReporteRequest req)
    {
        using var con = _db.CreateConnection();
        var desde = req.Desde ?? NexoApi.Common.ColombiaClock.Today.AddMonths(-3);
        var hasta = req.Hasta ?? NexoApi.Common.ColombiaClock.Today;
        var limite = req.Limite ?? 500;

        var sql = req.Tipo switch
        {
            "inventario_valorizado" => @"
                SELECT TOP (@Limite) t.Referencia AS SKU, t.Nombre AS Articulo,
                    CAST(ISNULL(SUM(s.CantidadActual), 0) AS DECIMAL(18,2)) AS Stock,
                    CAST(ISNULL(t.CostoPromedio, 0) AS DECIMAL(18,2)) AS CostoPromedio,
                    CAST(ISNULL(SUM(s.CantidadActual), 0) * ISNULL(t.CostoPromedio, 0) AS DECIMAL(18,2)) AS ValorTotal
                FROM Catalogo.Tarjetas t
                LEFT JOIN Inventario.InventarioStock s ON s.ArticuloID = t.ArticuloID
                WHERE t.Estado = 1
                GROUP BY t.ArticuloID, t.Referencia, t.Nombre, t.CostoPromedio
                ORDER BY ValorTotal DESC",

            "margen_utilidad" => @"
                SELECT TOP (@Limite) t.Referencia AS SKU, t.Nombre AS Articulo,
                    CAST(ISNULL(t.PPublico, 0) AS DECIMAL(18,2)) AS PrecioVenta,
                    CAST(ISNULL(t.CostoPromedio, 0) AS DECIMAL(18,2)) AS Costo,
                    CAST(ISNULL(t.PPublico, 0) - ISNULL(t.CostoPromedio, 0) AS DECIMAL(18,2)) AS UtilidadBruta,
                    CAST(CASE WHEN ISNULL(t.PPublico, 0) > 0
                         THEN (ISNULL(t.PPublico, 0) - ISNULL(t.CostoPromedio, 0)) / t.PPublico * 100
                         ELSE 0 END AS DECIMAL(18,2)) AS MargenPct
                FROM Catalogo.Tarjetas t
                WHERE t.Estado = 1
                ORDER BY MargenPct DESC",

            "kardex_individual" => @"
                SELECT TOP (@Limite) k.Fecha,
                    t.Referencia AS SKU, t.Nombre AS Articulo,
                    tm.Nombre AS TipoMovimiento,
                    CASE WHEN tm.Signo > 0 THEN CAST(k.Cantidad AS DECIMAL(18,2)) ELSE CAST(NULL AS DECIMAL(18,2)) END AS Entrada,
                    CASE WHEN tm.Signo < 0 THEN CAST(ABS(k.Cantidad) AS DECIMAL(18,2)) ELSE CAST(NULL AS DECIMAL(18,2)) END AS Salida,
                    CAST(k.CantidadSaldo AS DECIMAL(18,2)) AS Saldo,
                    CAST(k.CostoUnitario AS DECIMAL(18,2)) AS CostoUnitario,
                    ISNULL(k.ObservacionDetallada, '') AS Observacion,
                    ISNULL(u.Nombres + ' ' + u.Apellidos, '') AS Usuario
                FROM Kardex.KardexMovimientos k
                JOIN Catalogo.Tarjetas t ON t.ArticuloID = k.ArticuloID
                JOIN Kardex.TiposMovimientoKardex tm ON tm.TipoMovID = k.TipoMovID
                LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = k.UsuarioID
                WHERE k.Fecha BETWEEN @Desde AND @Hasta
                    AND (@Filtro IS NULL OR t.Referencia = @Filtro OR t.Nombre LIKE '%' + @Filtro + '%')
                ORDER BY k.Fecha DESC",

            "rendimiento_insumos" => @"
                SELECT TOP (@Limite)
                    op.CodigoOP AS OP,
                    pt.Nombre AS Producto,
                    CAST(op.CantidadProgramada AS DECIMAL(18,2)) AS CantidadProgramada,
                    CAST(ISNULL(op.CantidadProducidaReal, 0) AS DECIMAL(18,2)) AS CantidadProducida,
                    ins.Nombre AS Insumo,
                    CAST(c.CantidadTeorica AS DECIMAL(18,2)) AS CantidadTeorica,
                    CAST(c.CantidadReal AS DECIMAL(18,2)) AS CantidadReal,
                    CAST(c.CantidadReal - c.CantidadTeorica AS DECIMAL(18,2)) AS Desviacion,
                    CAST(CASE WHEN c.CantidadTeorica > 0
                         THEN (c.CantidadReal - c.CantidadTeorica) / c.CantidadTeorica * 100
                         ELSE 0 END AS DECIMAL(18,2)) AS DesviacionPct
                FROM Produccion.OrdenesProduccionConsumo c
                JOIN Produccion.OrdenesProduccion op ON op.OrdenProduccionID = c.OrdenProduccionID
                JOIN Catalogo.Tarjetas pt ON pt.ArticuloID = op.ProductoTerminadoID
                JOIN Catalogo.Tarjetas ins ON ins.ArticuloID = c.ArticuloID
                WHERE op.FechaCreacion BETWEEN @Desde AND @Hasta
                ORDER BY op.CodigoOP, ins.Nombre",

            "stock_actual" => @"
                SELECT TOP (@Limite) t.Referencia AS SKU, t.Nombre AS Articulo,
                    CAST(ISNULL(stk.Existencias, 0) AS DECIMAL(18,2)) AS Stock,
                    CAST(ISNULL(t.StockMinimo, 0) AS DECIMAL(18,2)) AS StockMinimo,
                    CASE WHEN ISNULL(stk.Existencias, 0) = 0 THEN 'Sin Stock'
                         WHEN ISNULL(stk.Existencias, 0) < ISNULL(t.StockMinimo, 0) THEN 'Bajo Stock'
                         ELSE 'OK' END AS Estado
                FROM Catalogo.Tarjetas t
                LEFT JOIN (
                    SELECT ArticuloID, SUM(CantidadActual) AS Existencias
                    FROM Inventario.InventarioStock
                    GROUP BY ArticuloID
                ) stk ON stk.ArticuloID = t.ArticuloID
                WHERE t.Estado = 1
                ORDER BY ISNULL(stk.Existencias, 0) ASC",

            "clientes_todos" => @"
                SELECT TOP (@Limite) c.Nombre AS Cliente, ISNULL(c.NIT,'') AS NIT,
                    ISNULL(c.TipoCliente,'') AS Tipo, ISNULL(c.Ciudad,'') AS Ciudad,
                    ISNULL(u.Nombres + ' ' + u.Apellidos,'') AS Responsable,
                    CASE WHEN c.Estado = 1 THEN 'Activo' ELSE 'Inactivo' END AS Estado
                FROM Crm.Clientes c
                LEFT JOIN Seguridad.Usuarios u ON u.UsuarioID = c.ResponsableID
                ORDER BY c.Nombre",

            "ventas_por_periodo" => @"
                SELECT TOP (@Limite) Fecha,
                    SUM(TotalNEXO)       AS TotalNEXO,
                    SUM(FacturasNEXO)    AS FacturasNEXO,
                    SUM(TotalVisions)    AS TotalVisions,
                    SUM(UnidadesVisions) AS UnidadesVisions
                FROM (
                    SELECT CAST(f.Fecha AS DATE) AS Fecha,
                        ISNULL(SUM(fl.Cantidad * fl.PrecioUnitario), 0) AS TotalNEXO,
                        COUNT(DISTINCT f.FacturaID)                      AS FacturasNEXO,
                        CAST(0 AS DECIMAL(18,2))                         AS TotalVisions,
                        CAST(0 AS DECIMAL(18,4))                         AS UnidadesVisions
                    FROM Facturacion.Facturas f
                    LEFT JOIN Facturacion.FacturaLineas fl ON fl.FacturaID = f.FacturaID
                    WHERE f.Fecha BETWEEN @Desde AND @Hasta
                    GROUP BY CAST(f.Fecha AS DATE)
                    UNION ALL
                    SELECT CAST(e.FechaEventoOrigen AS DATE),
                        CAST(0 AS DECIMAL(18,2)),
                        0,
                        SUM(ISNULL(e.PrecioArticuloVisions, 0) * e.Cantidad),
                        SUM(e.Cantidad)
                    FROM Integracion.EventosEntrantes e
                    WHERE e.Procesado = 1 AND e.TipoEvento = 'VENTA'
                        AND e.FechaEventoOrigen BETWEEN @Desde AND @Hasta
                    GROUP BY CAST(e.FechaEventoOrigen AS DATE)
                ) src
                GROUP BY Fecha
                ORDER BY Fecha DESC",

            "ventas_por_articulo" => @"
                SELECT TOP (@Limite)
                    a.Referencia AS SKU, a.Nombre AS Articulo,
                    CAST(ISNULL(nexo.CantidadNEXO, 0) AS DECIMAL(18,2))  AS CantidadNEXO,
                    CAST(ISNULL(vis.CantidadVisions, 0) AS DECIMAL(18,2)) AS CantidadVisions,
                    CAST(ISNULL(nexo.CantidadNEXO, 0) + ISNULL(vis.CantidadVisions, 0) AS DECIMAL(18,2)) AS CantidadTotal,
                    CAST(ISNULL(nexo.TotalNEXO, 0) + ISNULL(vis.TotalVisions, 0) AS DECIMAL(18,2)) AS TotalFacturado
                FROM Catalogo.Tarjetas a
                LEFT JOIN (
                    SELECT fl.ArticuloID,
                        SUM(fl.Cantidad)                     AS CantidadNEXO,
                        SUM(fl.Cantidad * fl.PrecioUnitario) AS TotalNEXO
                    FROM Facturacion.FacturaLineas fl
                    JOIN Facturacion.Facturas f ON f.FacturaID = fl.FacturaID
                    WHERE f.Fecha BETWEEN @Desde AND @Hasta
                    GROUP BY fl.ArticuloID
                ) nexo ON nexo.ArticuloID = a.ArticuloID
                LEFT JOIN (
                    SELECT COALESCE(m.ArticuloID, t2.ArticuloID) AS ArticuloID,
                        SUM(e.Cantidad) AS CantidadVisions,
                        SUM(ISNULL(e.PrecioArticuloVisions, 0) * e.Cantidad) AS TotalVisions
                    FROM Integracion.EventosEntrantes e
                    LEFT JOIN Integracion.MapeoArticulos m
                        ON m.CodigoArticuloVisions = e.CodigoArticuloVisions AND m.CentroCostoID = e.CentroCostoID
                    LEFT JOIN Catalogo.Tarjetas t2 ON t2.Referencia = e.CodigoArticuloVisions
                    WHERE e.Procesado = 1 AND e.TipoEvento = 'VENTA'
                        AND e.FechaEventoOrigen BETWEEN @Desde AND @Hasta
                    GROUP BY COALESCE(m.ArticuloID, t2.ArticuloID)
                ) vis ON vis.ArticuloID = a.ArticuloID
                WHERE a.Estado = 1 AND (nexo.ArticuloID IS NOT NULL OR vis.ArticuloID IS NOT NULL)
                ORDER BY CantidadTotal DESC",

            "ventas_por_cliente" => @"
                SELECT TOP (@Limite) Cliente, ISNULL(NIT,'') AS NIT,
                    SUM(TotalFacturado) AS TotalFacturado,
                    SUM(NumDocumentos)  AS NumDocumentos,
                    MAX(UltimaFactura)  AS UltimaFactura
                FROM (
                    SELECT c.Nombre AS Cliente, ISNULL(c.NIT,'') AS NIT,
                        ISNULL(SUM(fl.Cantidad * fl.PrecioUnitario), 0) AS TotalFacturado,
                        COUNT(DISTINCT f.FacturaID) AS NumDocumentos,
                        MAX(f.Fecha) AS UltimaFactura
                    FROM Facturacion.Facturas f
                    JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
                    LEFT JOIN Facturacion.FacturaLineas fl ON fl.FacturaID = f.FacturaID
                    WHERE f.Fecha BETWEEN @Desde AND @Hasta
                    GROUP BY c.ClienteID, c.Nombre, c.NIT
                    UNION ALL
                    SELECT ISNULL(e.NombreCliente, 'CONSUMIDOR FINAL'),
                        ISNULL(e.NitCliente, ''),
                        SUM(ISNULL(e.PrecioArticuloVisions, 0) * e.Cantidad),
                        COUNT(DISTINCT e.NroDoc),
                        MAX(e.FechaEventoOrigen)
                    FROM Integracion.EventosEntrantes e
                    WHERE e.Procesado = 1 AND e.TipoEvento = 'VENTA'
                        AND e.FechaEventoOrigen BETWEEN @Desde AND @Hasta
                    GROUP BY ISNULL(e.NombreCliente, 'CONSUMIDOR FINAL'), ISNULL(e.NitCliente, '')
                ) combined
                GROUP BY Cliente, NIT
                ORDER BY TotalFacturado DESC",

            "top_articulos" => @"
                SELECT TOP (@Limite)
                    a.Referencia AS SKU, a.Nombre AS Articulo,
                    CAST(ISNULL(nexo.Cantidad, 0) + ISNULL(vis.Cantidad, 0) AS DECIMAL(18,2)) AS CantidadTotal,
                    CAST(ISNULL(nexo.Total, 0) + ISNULL(vis.Total, 0) AS DECIMAL(18,2)) AS TotalFacturado,
                    CASE
                        WHEN ISNULL(nexo.Cantidad, 0) = 0 AND ISNULL(vis.Cantidad, 0) = 0 THEN 'Sin ventas'
                        WHEN ISNULL(vis.Cantidad, 0) >= ISNULL(nexo.Cantidad, 0) THEN 'POS Visions'
                        ELSE 'NEXO'
                    END AS CanalPrincipal
                FROM Catalogo.Tarjetas a
                LEFT JOIN (
                    SELECT fl.ArticuloID,
                        SUM(fl.Cantidad)                     AS Cantidad,
                        SUM(fl.Cantidad * fl.PrecioUnitario) AS Total
                    FROM Facturacion.FacturaLineas fl
                    JOIN Facturacion.Facturas f ON f.FacturaID = fl.FacturaID
                    WHERE f.Fecha BETWEEN @Desde AND @Hasta
                    GROUP BY fl.ArticuloID
                ) nexo ON nexo.ArticuloID = a.ArticuloID
                LEFT JOIN (
                    SELECT COALESCE(m.ArticuloID, t2.ArticuloID) AS ArticuloID,
                        SUM(e.Cantidad) AS Cantidad,
                        SUM(ISNULL(e.PrecioArticuloVisions, 0) * e.Cantidad) AS Total
                    FROM Integracion.EventosEntrantes e
                    LEFT JOIN Integracion.MapeoArticulos m
                        ON m.CodigoArticuloVisions = e.CodigoArticuloVisions AND m.CentroCostoID = e.CentroCostoID
                    LEFT JOIN Catalogo.Tarjetas t2 ON t2.Referencia = e.CodigoArticuloVisions
                    WHERE e.Procesado = 1 AND e.TipoEvento = 'VENTA'
                        AND e.FechaEventoOrigen BETWEEN @Desde AND @Hasta
                    GROUP BY COALESCE(m.ArticuloID, t2.ArticuloID)
                ) vis ON vis.ArticuloID = a.ArticuloID
                WHERE a.Estado = 1 AND (nexo.ArticuloID IS NOT NULL OR vis.ArticuloID IS NOT NULL)
                ORDER BY CantidadTotal DESC",

            "articulos_sin_movimiento" => @"
                SELECT TOP (@Limite)
                    t.Referencia AS SKU, t.Nombre AS Articulo,
                    CAST(ISNULL(SUM(stk.CantidadActual), 0) AS DECIMAL(18,2)) AS Stock,
                    MAX(k.Fecha) AS UltimoMovimiento,
                    ISNULL(DATEDIFF(DAY, MAX(k.Fecha), GETDATE()), 9999) AS DiasParado
                FROM Catalogo.Tarjetas t
                LEFT JOIN Kardex.KardexMovimientos k ON k.ArticuloID = t.ArticuloID
                LEFT JOIN Inventario.InventarioStock stk ON stk.ArticuloID = t.ArticuloID
                WHERE t.Estado = 1
                GROUP BY t.ArticuloID, t.Referencia, t.Nombre
                HAVING MAX(k.Fecha) < @Desde OR MAX(k.Fecha) IS NULL
                ORDER BY DiasParado DESC",

            "rotacion_inventario" => @"
                SELECT TOP (@Limite)
                    t.Referencia AS SKU, t.Nombre AS Articulo,
                    CAST(ISNULL(SUM(stk.CantidadActual), 0) AS DECIMAL(18,2)) AS Stock,
                    CAST(ISNULL(nexo.Cantidad, 0) + ISNULL(vis.Cantidad, 0) AS DECIMAL(18,2)) AS CantidadVendida,
                    CAST(CASE
                        WHEN ISNULL(SUM(stk.CantidadActual), 0) <= 0 THEN NULL
                        ELSE ROUND((ISNULL(nexo.Cantidad,0) + ISNULL(vis.Cantidad,0))
                             / NULLIF(SUM(stk.CantidadActual), 0), 2)
                    END AS DECIMAL(18,2)) AS Rotacion,
                    CASE
                        WHEN ISNULL(nexo.Cantidad,0) + ISNULL(vis.Cantidad,0) <= 0 THEN NULL
                        ELSE CAST(ROUND(
                            DATEDIFF(DAY, @Desde, @Hasta)
                            * ISNULL(SUM(stk.CantidadActual), 0)
                            / NULLIF(ISNULL(nexo.Cantidad,0) + ISNULL(vis.Cantidad,0), 0)
                        , 0) AS INT)
                    END AS DiasInventario
                FROM Catalogo.Tarjetas t
                LEFT JOIN Inventario.InventarioStock stk ON stk.ArticuloID = t.ArticuloID
                LEFT JOIN (
                    SELECT fl.ArticuloID, SUM(fl.Cantidad) AS Cantidad
                    FROM Facturacion.FacturaLineas fl
                    JOIN Facturacion.Facturas f ON f.FacturaID = fl.FacturaID
                    WHERE f.Fecha BETWEEN @Desde AND @Hasta
                    GROUP BY fl.ArticuloID
                ) nexo ON nexo.ArticuloID = t.ArticuloID
                LEFT JOIN (
                    SELECT COALESCE(m.ArticuloID, t2.ArticuloID) AS ArticuloID, SUM(e.Cantidad) AS Cantidad
                    FROM Integracion.EventosEntrantes e
                    LEFT JOIN Integracion.MapeoArticulos m
                        ON m.CodigoArticuloVisions = e.CodigoArticuloVisions AND m.CentroCostoID = e.CentroCostoID
                    LEFT JOIN Catalogo.Tarjetas t2 ON t2.Referencia = e.CodigoArticuloVisions
                    WHERE e.Procesado = 1 AND e.TipoEvento = 'VENTA'
                        AND e.FechaEventoOrigen BETWEEN @Desde AND @Hasta
                    GROUP BY COALESCE(m.ArticuloID, t2.ArticuloID)
                ) vis ON vis.ArticuloID = t.ArticuloID
                WHERE t.Estado = 1
                GROUP BY t.ArticuloID, t.Referencia, t.Nombre, nexo.Cantidad, vis.Cantidad
                ORDER BY Rotacion DESC",

            "clientes_nuevos" => @"
                SELECT TOP (@Limite) c.Nombre AS Cliente, ISNULL(c.NIT,'') AS NIT,
                    ISNULL(c.TipoCliente,'') AS Tipo, ISNULL(c.Ciudad,'') AS Ciudad,
                    ISNULL(c.FechaCreacion, c.FechaModificacion) AS FechaRegistro
                FROM Crm.Clientes c
                WHERE ISNULL(c.FechaCreacion, c.FechaModificacion) BETWEEN @Desde AND @Hasta
                ORDER BY ISNULL(c.FechaCreacion, c.FechaModificacion) DESC",

            "tickets_por_estado" => @"
                SELECT Estado, Prioridad, COUNT(*) AS Total
                FROM Soporte.Tickets
                WHERE FechaCreacion BETWEEN @Desde AND @Hasta
                GROUP BY Estado, Prioridad
                ORDER BY
                    CASE Estado WHEN 'ABIERTO' THEN 1 WHEN 'EN_PROGRESO' THEN 2 WHEN 'RESUELTO' THEN 3 ELSE 4 END,
                    CASE Prioridad WHEN 'CRITICA' THEN 1 WHEN 'ALTA' THEN 2 WHEN 'MEDIA' THEN 3 ELSE 4 END",

            "oportunidades_embudo" => @"
                SELECT Etapa, COUNT(*) AS Total, SUM(ISNULL(ValorEstimado,0)) AS ValorEstimado
                FROM Crm.Oportunidades
                WHERE Etapa NOT IN ('GANADA','PERDIDA')
                GROUP BY Etapa
                ORDER BY CASE Etapa
                    WHEN 'PROSPECCION' THEN 1 WHEN 'CALIFICACION' THEN 2
                    WHEN 'PROPUESTA' THEN 3 WHEN 'NEGOCIACION' THEN 4 ELSE 5 END",

            "nps_resumen" => @"
                SELECT YEAR(FechaRespuesta) AS Anio, MONTH(FechaRespuesta) AS Mes,
                    AVG(CAST(Puntuacion AS FLOAT)) AS Promedio,
                    COUNT(*) AS Total,
                    SUM(CASE WHEN Puntuacion >= 9 THEN 1 ELSE 0 END) AS Promotores,
                    SUM(CASE WHEN Puntuacion <= 6 THEN 1 ELSE 0 END) AS Detractores
                FROM Soporte.EncuestasNPS
                WHERE FechaRespuesta BETWEEN @Desde AND @Hasta
                GROUP BY YEAR(FechaRespuesta), MONTH(FechaRespuesta)
                ORDER BY Anio DESC, Mes DESC",

            _ => throw new ArgumentException($"Tipo de reporte no reconocido: {req.Tipo}")
        };

        var filtro = string.IsNullOrWhiteSpace(req.Filtro) ? null : req.Filtro.Trim();
        var rows = await con.QueryAsync(sql, new { Desde = desde, Hasta = hasta, Limite = limite, Filtro = filtro });
        return rows.Select(r =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in (IDictionary<string, object>)r)
                dict[prop.Key] = prop.Value is DBNull ? null : prop.Value;
            return dict;
        });
    }

    public async Task<byte[]> ExportarExcelAsync(ReporteRequest req)
    {
        var filas = (await EjecutarAsync(req)).ToList();
        var info  = _reportes.FirstOrDefault(r => r.Tipo == req.Tipo);
        return ExportService.GenerarExcelReporte(info?.Nombre ?? req.Tipo, req.Desde, req.Hasta, filas);
    }

    public async Task<byte[]> ExportarPdfAsync(ReporteRequest req, string empresa)
    {
        var filas = (await EjecutarAsync(req)).ToList();
        var info  = _reportes.FirstOrDefault(r => r.Tipo == req.Tipo);
        return ExportService.GenerarPdfReporte(empresa, info?.Nombre ?? req.Tipo, info?.Descripcion, req.Desde, req.Hasta, filas);
    }
}
