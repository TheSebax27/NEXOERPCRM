using System.Security.Claims;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Crm;
using NexoApi.Features.Notificaciones.Dtos;
using NexoApi.Features.Planificacion;
using NexoApi.Features.Proyectos;

namespace NexoApi.Features.Notificaciones;

public interface INotificacionesService
{
    Task<ResumenNotificaciones> ObtenerResumenAsync(ClaimsPrincipal usuario);
}

// Notificaciones "agregadas por categoria" a proposito -- no una fila por
// cada articulo con bajo stock (seria una lista larga y molesta), sino un
// resumen con conteo por categoria (Sin Stock, Bajo Stock, Produccion en
// Proceso, y desde agosto 2026 las alertas ya construidas en BI) y un link
// a la pantalla correspondiente para ver el detalle.
public class NotificacionesService : INotificacionesService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICrmService _crm;
    private readonly IPlanificacionService _planificacion;
    private readonly IProyectosService _proyectos;

    public NotificacionesService(IDbConnectionFactory db, ICrmService crm, IPlanificacionService planificacion, IProyectosService proyectos)
    {
        _db = db;
        _crm = crm;
        _planificacion = planificacion;
        _proyectos = proyectos;
    }

    public async Task<ResumenNotificaciones> ObtenerResumenAsync(ClaimsPrincipal usuario)
    {
        using var connection = _db.CreateConnection();
        var items = new List<NotificacionItem>();

        // Cada categoria se evalua en su propio try/catch: si una falla (SP no existe,
        // permiso, timeout), las demas siguen mostrando. Nunca 500 por una categoria sola.

        try
        {
            const string sqlSinStock = @"
                SELECT g.Articulo
                FROM (
                    SELECT ArticuloID, MAX(Articulo) AS Articulo, SUM(CantidadActual) AS TotalStock
                    FROM Inventario.vw_StockConsolidado
                    GROUP BY ArticuloID
                ) g
                WHERE g.TotalStock = 0
                ORDER BY g.Articulo";
            var sinStock = (await connection.QueryAsync<string>(sqlSinStock)).ToList();
            if (sinStock.Count > 0)
                items.Add(new NotificacionItem("SinStock", "error",
                    $"{sinStock.Count} artículo{(sinStock.Count == 1 ? "" : "s")} sin stock",
                    ResumirNombres(sinStock), "/inventario/stock"));
        }
        catch { }

        try
        {
            const string sqlBajoStock = @"
                SELECT g.Articulo
                FROM (
                    SELECT s.ArticuloID, MAX(s.Articulo) AS Articulo, SUM(s.CantidadActual) AS TotalStock
                    FROM Inventario.vw_StockConsolidado s
                    GROUP BY s.ArticuloID
                ) g
                JOIN Catalogo.Tarjetas a ON a.ArticuloID = g.ArticuloID
                WHERE a.StockMinimo > 0
                  AND g.TotalStock > 0
                  AND g.TotalStock <= a.StockMinimo
                ORDER BY g.Articulo";
            var bajoStock = (await connection.QueryAsync<string>(sqlBajoStock)).ToList();
            if (bajoStock.Count > 0)
                items.Add(new NotificacionItem("BajoStock", "warning",
                    $"{bajoStock.Count} artículo{(bajoStock.Count == 1 ? "" : "s")} bajo el stock mínimo",
                    ResumirNombres(bajoStock), "/inventario/stock"));
        }
        catch { }

        // Bajo stock sin OC abierta
        try
        {
            const string sqlBajoSinOC = @"
                SELECT a.Nombre
                FROM Catalogo.Tarjetas a
                JOIN (
                    SELECT s.ArticuloID, SUM(s.CantidadActual) AS TotalStock
                    FROM Inventario.vw_StockConsolidado s
                    GROUP BY s.ArticuloID
                ) stock ON stock.ArticuloID = a.ArticuloID
                WHERE a.StockMinimo > 0
                  AND stock.TotalStock <= a.StockMinimo
                  AND NOT EXISTS (
                      SELECT 1 FROM Compras.OrdenesCompraDetalle ocl
                      JOIN Compras.OrdenesCompra oc ON oc.OrdenCompraID = ocl.OrdenCompraID
                      WHERE ocl.ArticuloID = a.ArticuloID
                        AND oc.EstadoOC NOT IN ('RECIBIDA','CERRADA','CANCELADA','ANULADA')
                  )
                ORDER BY a.Nombre";
            var bajoSinOC = (await connection.QueryAsync<string>(sqlBajoSinOC)).ToList();
            if (bajoSinOC.Count > 0)
                items.Add(new NotificacionItem("BajoSinOC", "error",
                    $"{bajoSinOC.Count} artículo{(bajoSinOC.Count == 1 ? "" : "s")} bajo mínimo sin orden de compra",
                    ResumirNombres(bajoSinOC), "/compras/ordenes"));
        }
        catch { }

        try
        {
            const string sqlEnProceso = @"
                SELECT op.CodigoOP
                FROM Produccion.OrdenesProduccion op
                JOIN Produccion.EstadosOP e ON e.EstadoOPID = op.EstadoOPID
                WHERE e.Nombre = 'En Proceso'
                ORDER BY op.FechaInicio DESC";
            var enProceso = (await connection.QueryAsync<string>(sqlEnProceso)).ToList();
            if (enProceso.Count > 0)
                items.Add(new NotificacionItem("EnProceso", "info",
                    $"{enProceso.Count} orden{(enProceso.Count == 1 ? "" : "es")} de producción en proceso",
                    ResumirNombres(enProceso), "/produccion/ordenes"));
        }
        catch { }

        if (usuario.IsInRole("Administracion"))
        {
            try
            {
                var clientesFrios = (await _crm.ListarClientesFriosAsync(30)).ToList();
                if (clientesFrios.Count > 0)
                    items.Add(new NotificacionItem("ClientesFrios", "warning",
                        $"{clientesFrios.Count} cliente{(clientesFrios.Count == 1 ? "" : "s")} sin contacto reciente",
                        ResumirNombres(clientesFrios.Select(c => c.Nombre).ToList()), "/crm/clientes"));
            }
            catch { }
        }

        if (usuario.IsInRole("Administracion") || usuario.IsInRole("Jefes"))
        {
            try
            {
                var desviaciones = (await _planificacion.ListarDesviacionesAsync(70)).ToList();
                if (desviaciones.Count > 0)
                    items.Add(new NotificacionItem("DesviacionesPlanificacion", "warning",
                        $"{desviaciones.Count} centro{(desviaciones.Count == 1 ? "" : "s")} de costo por debajo de su meta",
                        ResumirNombres(desviaciones.Select(d => d.CentroCosto).ToList()), "/planificacion/demanda"));
            }
            catch { }

            try
            {
                var alertasProyectos = (await _proyectos.ListarAlertasAsync()).ToList();
                if (alertasProyectos.Count > 0)
                    items.Add(new NotificacionItem("AlertasProyectos", "error",
                        $"{alertasProyectos.Count} proyecto{(alertasProyectos.Count == 1 ? "" : "s")} atrasado{(alertasProyectos.Count == 1 ? "" : "s")} o con sobrecosto",
                        ResumirNombres(alertasProyectos.Select(p => p.Nombre).ToList()), "/proyectos"));
            }
            catch { }
        }

        // A1 — Maquinaria con mantenimiento vencido
        try
        {
            const string sqlMantVencido = @"
                SELECT m.Nombre + ' (' + m.Codigo + ')'
                FROM Produccion.Maquinaria m
                WHERE m.Estado NOT IN ('Inactiva', 'BajaDefinitiva')
                  AND (
                      SELECT TOP 1 mm.ProximoMantenimiento
                      FROM Produccion.MantenimientoMaquinaria mm
                      WHERE mm.MaquinariaID = m.MaquinariaID AND mm.ProximoMantenimiento IS NOT NULL
                      ORDER BY mm.FechaRealizado DESC
                  ) < CAST(GETDATE() AS DATE)
                ORDER BY m.Nombre";
            var mantVencido = (await connection.QueryAsync<string>(sqlMantVencido)).ToList();
            if (mantVencido.Count > 0)
                items.Add(new NotificacionItem("MantVencido", "error",
                    $"{mantVencido.Count} máquina{(mantVencido.Count == 1 ? "" : "s")} con mantenimiento vencido",
                    ResumirNombres(mantVencido), "/produccion/maquinaria"));
        }
        catch { }

        // A2 — Maquinaria con mantenimiento próximo (≤7 días)
        try
        {
            const string sqlMantProximo = @"
                SELECT m.Nombre + ' (' + m.Codigo + ')'
                FROM Produccion.Maquinaria m
                WHERE m.Estado NOT IN ('Inactiva', 'BajaDefinitiva')
                  AND (
                      SELECT TOP 1 mm.ProximoMantenimiento
                      FROM Produccion.MantenimientoMaquinaria mm
                      WHERE mm.MaquinariaID = m.MaquinariaID AND mm.ProximoMantenimiento IS NOT NULL
                      ORDER BY mm.FechaRealizado DESC
                  ) BETWEEN CAST(GETDATE() AS DATE) AND DATEADD(day, 7, CAST(GETDATE() AS DATE))
                ORDER BY m.Nombre";
            var mantProximo = (await connection.QueryAsync<string>(sqlMantProximo)).ToList();
            if (mantProximo.Count > 0)
                items.Add(new NotificacionItem("MantProximo", "warning",
                    $"{mantProximo.Count} máquina{(mantProximo.Count == 1 ? "" : "s")} con mantenimiento en los próximos 7 días",
                    ResumirNombres(mantProximo), "/produccion/maquinaria"));
        }
        catch { }

        // B — Lotes próximos a vencer (≤30 días, con stock > 0)
        try
        {
            const string sqlLotesVencer = @"
                SELECT DISTINCT a.Nombre + ' (Lote: ' + l.NumeroLote + ')'
                FROM Inventario.vw_StockConsolidado s
                JOIN Inventario.Lotes l ON l.LoteID = s.LoteID
                JOIN Catalogo.Tarjetas a ON a.ArticuloID = s.ArticuloID
                WHERE l.FechaVencimiento IS NOT NULL
                  AND l.FechaVencimiento >= CAST(GETDATE() AS DATE)
                  AND l.FechaVencimiento <= DATEADD(day, 30, CAST(GETDATE() AS DATE))
                  AND s.CantidadActual > 0
                ORDER BY a.Nombre + ' (Lote: ' + l.NumeroLote + ')'";
            var lotesVencer = (await connection.QueryAsync<string>(sqlLotesVencer)).ToList();
            if (lotesVencer.Count > 0)
                items.Add(new NotificacionItem("LotesVencer", "warning",
                    $"{lotesVencer.Count} lote{(lotesVencer.Count == 1 ? "" : "s")} próximo{(lotesVencer.Count == 1 ? "" : "s")} a vencer (30 días)",
                    ResumirNombres(lotesVencer), "/inventario/stock"));
        }
        catch { }

        // C — Órdenes de producción retrasadas (FechaPlanificada vencida, aún abiertas)
        try
        {
            const string sqlOPRetrasadas = @"
                SELECT op.CodigoOP
                FROM Produccion.OrdenesProduccion op
                JOIN Produccion.EstadosOP e ON e.EstadoOPID = op.EstadoOPID
                WHERE e.Nombre IN ('En Proceso', 'Planificada', 'Retrasada')
                  AND op.FechaPlanificada < CAST(GETDATE() AS DATE)
                ORDER BY op.FechaPlanificada";
            var opRetrasadas = (await connection.QueryAsync<string>(sqlOPRetrasadas)).ToList();
            if (opRetrasadas.Count > 0)
                items.Add(new NotificacionItem("OPRetrasada", "error",
                    $"{opRetrasadas.Count} orden{(opRetrasadas.Count == 1 ? "" : "es")} de producción retrasada{(opRetrasadas.Count == 1 ? "" : "s")}",
                    ResumirNombres(opRetrasadas), "/produccion/ordenes"));
        }
        catch { }

        // D — Facturas DIAN sin NroDoc (pendientes de numeración)
        try
        {
            const string sqlDianPendientes = @"
                SELECT c.Nombre + ' (F#' + CAST(f.FacturaID AS NVARCHAR) + ')'
                FROM Facturacion.Facturas f
                JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
                WHERE f.TipDoc = 'FACTURA'
                  AND f.NroDoc IS NULL
                ORDER BY f.Fecha DESC";
            var dianPendientes = (await connection.QueryAsync<string>(sqlDianPendientes)).ToList();
            if (dianPendientes.Count > 0)
                items.Add(new NotificacionItem("DianPendiente", "warning",
                    $"{dianPendientes.Count} factura{(dianPendientes.Count == 1 ? "" : "s")} sin número DIAN",
                    ResumirNombres(dianPendientes), "/facturacion/facturas"));
        }
        catch { }

        try
        {
            const string sqlProximasVencer = @"
                SELECT cl.Nombre
                FROM Crm.Cotizaciones c
                JOIN Crm.Clientes cl ON cl.ClienteID = c.ClienteID
                WHERE c.ValidoHasta IS NOT NULL
                  AND c.ValidoHasta >= CAST(GETDATE() AS DATE)
                  AND c.ValidoHasta <= DATEADD(day, 3, CAST(GETDATE() AS DATE))
                  AND c.Estado NOT IN ('ACEPTADA','RECHAZADA','CONVERTIDA','VENCIDA')
                ORDER BY c.ValidoHasta";
            var proximasVencer = (await connection.QueryAsync<string>(sqlProximasVencer)).ToList();
            if (proximasVencer.Count > 0)
                items.Add(new NotificacionItem("ProximaVencer", "warning",
                    $"{proximasVencer.Count} cotización{(proximasVencer.Count == 1 ? "" : "es")} por vencer en 3 días",
                    ResumirNombres(proximasVencer), "/crm/cotizaciones"));
        }
        catch { }

        // E — Artículos que se agotan en ≤7 días al ritmo actual
        try
        {
            const string sqlPorAgotarse = @"
                WITH Consumo AS (
                    SELECT ArticuloID,
                           CAST(SUM(ABS(Cantidad)) / 30.0 AS decimal(18,6)) AS ConsumoDiario
                    FROM Kardex.KardexMovimientos
                    WHERE Cantidad < 0
                      AND Fecha >= DATEADD(day, -30, GETDATE())
                    GROUP BY ArticuloID
                ),
                StockTotal AS (
                    SELECT ArticuloID, SUM(CantidadActual) AS Stock
                    FROM Inventario.vw_StockConsolidado
                    GROUP BY ArticuloID
                )
                SELECT a.Nombre
                FROM StockTotal st
                JOIN Consumo c ON c.ArticuloID = st.ArticuloID
                JOIN Catalogo.Tarjetas a ON a.ArticuloID = st.ArticuloID
                WHERE st.Stock > 0
                  AND c.ConsumoDiario > 0
                  AND CAST(st.Stock / c.ConsumoDiario AS int) <= 7
                ORDER BY CAST(st.Stock / c.ConsumoDiario AS int)";
            var porAgotarse = (await connection.QueryAsync<string>(sqlPorAgotarse)).ToList();
            if (porAgotarse.Count > 0)
                items.Add(new NotificacionItem("PorAgotarse", "error",
                    $"{porAgotarse.Count} artículo{(porAgotarse.Count == 1 ? "" : "s")} por agotarse en ≤7 días",
                    ResumirNombres(porAgotarse), "/inventario/stock"));
        }
        catch { }

        try
        {
            const string sqlOpVencidas = @"
                SELECT o.Nombre + ' (' + ISNULL(c.Nombre, 'Sin cliente') + ')'
                FROM Crm.Oportunidades o
                LEFT JOIN Crm.Clientes c ON c.ClienteID = o.ClienteID
                WHERE o.FechaCierreEsperada < CAST(GETDATE() AS DATE)
                  AND o.Etapa NOT IN ('GANADA','PERDIDA')
                ORDER BY o.FechaCierreEsperada";
            var opVencidas = (await connection.QueryAsync<string>(sqlOpVencidas)).ToList();
            if (opVencidas.Count > 0)
                items.Add(new NotificacionItem("OportunidadesVencidas", "error",
                    $"{opVencidas.Count} oportunidad{(opVencidas.Count == 1 ? "" : "es")} con cierre vencido",
                    ResumirNombres(opVencidas), "/crm/pipeline"));
        }
        catch { }

        return new ResumenNotificaciones(items.Count, items);
    }

    private static string ResumirNombres(List<string> nombres)
    {
        const int max = 3;
        if (nombres.Count <= max)
            return string.Join(", ", nombres.Where(n => !string.IsNullOrWhiteSpace(n)));
        // Prefer names with at least one letter (filter out bad data like "0 0 0")
        var conLetras = nombres.Where(n => n.Any(char.IsLetter)).ToList();
        var preview = (conLetras.Count >= max ? conLetras : nombres).Take(max).ToList();
        return $"{string.Join(", ", preview)} y {nombres.Count - preview.Count} más";
    }
}
