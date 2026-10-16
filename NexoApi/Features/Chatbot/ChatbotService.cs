using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Chatbot.Dtos;
using NexoApi.Features.Configuracion;

namespace NexoApi.Features.Chatbot;

public interface IChatbotService
{
    Task<ChatResponse> ChatAsync(ChatRequest req);
}

public class ChatbotService : IChatbotService
{
    private readonly IDbConnectionFactory    _db;
    private readonly IConfiguracionService  _config;
    private readonly HttpClient              _http;
    private readonly IConfiguration         _cfg;
    private readonly ILogger<ChatbotService> _log;

    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public ChatbotService(
        IDbConnectionFactory db,
        IConfiguracionService config,
        HttpClient http,
        IConfiguration cfg,
        ILogger<ChatbotService> log)
    {
        _db     = db;
        _config = config;
        _http   = http;
        _cfg    = cfg;
        _log    = log;
    }

    // -- Tool definitions -------------------------------------------------------

    private static readonly JsonArray _tools = JsonNode.Parse("""
    [
      { "type":"function","function":{ "name":"resumen_hoy","description":"Ventas/facturas/ticket de hoy.","parameters":{"type":"object","properties":{},"required":[]}}},
      { "type":"function","function":{ "name":"ventas_periodo","description":"Ventas entre dos fechas.","parameters":{"type":"object","properties":{"desde":{"type":"string"},"hasta":{"type":"string"}},"required":["desde","hasta"]}}},
      { "type":"function","function":{ "name":"top_articulos","description":"Articulos mas vendidos en periodo.","parameters":{"type":"object","properties":{"desde":{"type":"string"},"hasta":{"type":"string"},"limite":{"type":"integer"}},"required":["desde","hasta"]}}},
      { "type":"function","function":{ "name":"consultar_stock","description":"Stock actual; filtro por nombre o solo criticos.","parameters":{"type":"object","properties":{"filtro":{"type":"string"},"solo_criticos":{"type":"boolean"}},"required":[]}}},
      { "type":"function","function":{ "name":"facturas_recientes","description":"Ultimas facturas.","parameters":{"type":"object","properties":{"limite":{"type":"integer"}},"required":[]}}},
      { "type":"function","function":{ "name":"kpis_mes","description":"KPIs del mes: ventas, facturas, variacion vs anterior.","parameters":{"type":"object","properties":{},"required":[]}}},
      { "type":"function","function":{ "name":"clientes_top","description":"Mejores clientes por monto en periodo.","parameters":{"type":"object","properties":{"desde":{"type":"string"},"hasta":{"type":"string"},"limite":{"type":"integer"}},"required":["desde","hasta"]}}},
      { "type":"function","function":{ "name":"buscar_articulo","description":"Articulo por nombre o referencia: precio, costo, margen, stock.","parameters":{"type":"object","properties":{"busqueda":{"type":"string"}},"required":["busqueda"]}}},
      { "type":"function","function":{ "name":"ventas_por_canal","description":"Ventas por tipo de documento en periodo.","parameters":{"type":"object","properties":{"desde":{"type":"string"},"hasta":{"type":"string"}},"required":["desde","hasta"]}}},
      { "type":"function","function":{ "name":"ordenes_produccion","description":"Ordenes de produccion activas o recientes.","parameters":{"type":"object","properties":{"solo_activas":{"type":"boolean"},"limite":{"type":"integer"}},"required":[]}}},
      { "type":"function","function":{ "name":"valor_inventario","description":"Valor del inventario por categoria.","parameters":{"type":"object","properties":{},"required":[]}}},
      { "type":"function","function":{ "name":"comparar_periodos","description":"Compara ventas entre dos periodos.","parameters":{"type":"object","properties":{"periodo1_desde":{"type":"string"},"periodo1_hasta":{"type":"string"},"periodo2_desde":{"type":"string"},"periodo2_hasta":{"type":"string"}},"required":["periodo1_desde","periodo1_hasta","periodo2_desde","periodo2_hasta"]}}},
      { "type":"function","function":{ "name":"detalle_factura","description":"Detalle de una factura por ID.","parameters":{"type":"object","properties":{"factura_id":{"type":"integer"}},"required":["factura_id"]}}},
      { "type":"function","function":{ "name":"facturas_cliente","description":"Facturas de un cliente por nombre o NIT.","parameters":{"type":"object","properties":{"cliente":{"type":"string"},"desde":{"type":"string"},"hasta":{"type":"string"},"limite":{"type":"integer"}},"required":["cliente"]}}},
      { "type":"function","function":{ "name":"margen_productos","description":"Articulos por margen de utilidad.","parameters":{"type":"object","properties":{"orden":{"type":"string","enum":["mejor","peor"]},"limite":{"type":"integer"}},"required":[]}}}
    ]
    """)!.AsArray();

    // -- Ejecucion de tools -----------------------------------------------------

    private static DateTime ParseFecha(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor) || !DateTime.TryParse(valor, out var dt))
            throw new ArgumentException($"Fecha invalida en '{campo}': '{valor}'. Usa formato yyyy-MM-dd.");
        return dt;
    }

    private async Task<string> EjecutarToolAsync(string nombre, JsonObject args)
    {
        using var con = _db.CreateConnection();

        switch (nombre)
        {
            case "resumen_hoy":
            {
                var hoy = NexoApi.Common.ColombiaClock.Today;
                var row = await con.QueryFirstOrDefaultAsync<dynamic>("""
                    SELECT ISNULL(SUM(tot.Total),0)  AS TotalVentas,
                           COUNT(*)                  AS TotalFacturas,
                           ISNULL(AVG(tot.Total),0)  AS TicketPromedio
                    FROM Facturacion.Facturas f
                    JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    WHERE CAST(f.Fecha AS date) = @Hoy
                    """, new { Hoy = hoy });
                return $"Ventas hoy ({hoy:dd/MM/yyyy}): ${(decimal)row.TotalVentas:N0} | " +
                       $"Facturas: {(int)row.TotalFacturas} | " +
                       $"Ticket promedio: ${(decimal)row.TicketPromedio:N0}";
            }

            case "ventas_periodo":
            {
                var desde = ParseFecha(args["desde"]?.GetValue<string>(), "desde");
                var hasta = ParseFecha(args["hasta"]?.GetValue<string>(), "hasta");
                var row = await con.QueryFirstOrDefaultAsync<dynamic>("""
                    SELECT ISNULL(SUM(tot.Total),0)  AS TotalVentas,
                           COUNT(*)                  AS TotalFacturas,
                           ISNULL(AVG(tot.Total),0)  AS TicketPromedio
                    FROM Facturacion.Facturas f
                    JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    WHERE f.Fecha BETWEEN @Desde AND @Hasta
                    """, new { Desde = desde, Hasta = hasta });
                return $"Ventas {desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}: " +
                       $"${(decimal)row.TotalVentas:N0} | " +
                       $"Facturas: {(int)row.TotalFacturas} | " +
                       $"Ticket promedio: ${(decimal)row.TicketPromedio:N0}";
            }

            case "top_articulos":
            {
                var desde  = ParseFecha(args["desde"]?.GetValue<string>(), "desde");
                var hasta  = ParseFecha(args["hasta"]?.GetValue<string>(), "hasta");
                var limite = args["limite"]?.GetValue<int>() ?? 10;
                var rows = await con.QueryAsync<dynamic>("""
                    SELECT TOP(@Limite)
                           t.Nombre        AS Articulo,
                           t.Referencia    AS SKU,
                           SUM(fl.Cantidad)                     AS Cantidad,
                           SUM(fl.Cantidad * fl.PrecioUnitario) AS Total
                    FROM Facturacion.FacturaLineas fl
                    JOIN Facturacion.Facturas  f ON f.FacturaID  = fl.FacturaID
                    JOIN Catalogo.Tarjetas     t ON t.ArticuloID = fl.ArticuloID
                    WHERE f.Fecha BETWEEN @Desde AND @Hasta
                    GROUP BY t.ArticuloID, t.Nombre, t.Referencia
                    ORDER BY Total DESC
                    """, new { Desde = desde, Hasta = hasta, Limite = limite });
                var lista = rows.ToList();
                if (!lista.Any()) return "Sin ventas en el periodo.";
                var sb = new StringBuilder($"Top {lista.Count} articulos ({desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}):\n");
                int i = 1;
                foreach (var r in lista)
                    sb.AppendLine($"{i++}. {r.Articulo} [{r.SKU}] - {(decimal)r.Cantidad:N0} uds - ${(decimal)r.Total:N0}");
                return sb.ToString();
            }

            case "consultar_stock":
            {
                var filtro       = args["filtro"]?.GetValue<string>();
                var soloCriticos = args["solo_criticos"]?.GetValue<bool>() ?? false;
                var rows = await con.QueryAsync<dynamic>("""
                    SELECT t.Referencia AS SKU,
                           t.Nombre,
                           ISNULL(s.CantidadActual, 0) AS Stock,
                           t.StockMinimo
                    FROM Catalogo.Tarjetas t
                    LEFT JOIN Inventario.InventarioStock s ON s.ArticuloID = t.ArticuloID
                    WHERE t.Estado = 1
                      AND (@Filtro IS NULL OR t.Nombre LIKE '%' + @Filtro + '%' OR t.Referencia = @Filtro)
                      AND (@SoloCriticos = 0 OR ISNULL(s.CantidadActual,0) <= t.StockMinimo)
                    ORDER BY Stock ASC
                    """, new { Filtro = filtro, SoloCriticos = soloCriticos ? 1 : 0 });
                var lista = rows.ToList();
                if (!lista.Any()) return "No se encontraron articulos con ese filtro.";
                var sb = new StringBuilder(soloCriticos ? "Articulos con stock critico:\n" : "Stock de articulos:\n");
                foreach (var r in lista)
                {
                    string alerta = (decimal)r.Stock <= (decimal)r.StockMinimo ? " [BAJO]" : "";
                    sb.AppendLine($"* {r.Nombre} [{r.SKU}]: {(decimal)r.Stock:N0} (min: {(decimal)r.StockMinimo:N0}){alerta}");
                }
                return sb.ToString();
            }

            case "facturas_recientes":
            {
                var limite = args["limite"]?.GetValue<int>() ?? 10;
                var rows = await con.QueryAsync<dynamic>("""
                    SELECT TOP(@Limite)
                           f.FacturaID,
                           f.Fecha,
                           ISNULL(c.Nombre, 'Consumidor Final') AS Cliente,
                           ISNULL(tot.Total, 0)                 AS Total,
                           f.TipDoc                             AS TipoDoc
                    FROM Facturacion.Facturas f
                    LEFT JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
                    LEFT JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    ORDER BY f.Fecha DESC, f.FacturaID DESC
                    """, new { Limite = limite });
                var lista = rows.ToList();
                if (!lista.Any()) return "Sin facturas registradas.";
                var sb = new StringBuilder($"Ultimas {lista.Count} facturas:\n");
                foreach (var r in lista)
                    sb.AppendLine($"* #{r.FacturaID} - {((DateTime)r.Fecha):dd/MM/yyyy} - {r.Cliente} - ${(decimal)r.Total:N0} [{r.TipoDoc}]");
                return sb.ToString();
            }

            case "kpis_mes":
            {
                var hoy          = NexoApi.Common.ColombiaClock.Today;
                var inicioMes    = new DateTime(hoy.Year, hoy.Month, 1);
                var inicioMesAnt = inicioMes.AddMonths(-1);
                var finMesAnt    = inicioMes.AddDays(-1);

                var mes = await con.QueryFirstOrDefaultAsync<dynamic>("""
                    SELECT ISNULL(SUM(tot.Total),0) AS TotalMes, COUNT(*) AS FacturasMes
                    FROM Facturacion.Facturas f
                    JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    WHERE f.Fecha >= @Inicio AND f.Fecha <= @Hoy
                    """, new { Inicio = inicioMes, Hoy = hoy });

                var mesAnt = await con.QueryFirstOrDefaultAsync<dynamic>("""
                    SELECT ISNULL(SUM(tot.Total),0) AS TotalMes, COUNT(*) AS FacturasMes
                    FROM Facturacion.Facturas f
                    JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    WHERE f.Fecha BETWEEN @Inicio AND @Fin
                    """, new { Inicio = inicioMesAnt, Fin = finMesAnt });

                decimal totalMes    = (decimal)mes.TotalMes;
                decimal totalAnt    = (decimal)mesAnt.TotalMes;
                decimal crecimiento = totalAnt > 0 ? (totalMes - totalAnt) / totalAnt * 100m : 0m;
                string tendencia    = crecimiento >= 0 ? $"^ +{crecimiento:N1}%" : $"v {crecimiento:N1}%";

                return $"KPIs {hoy:MMMM yyyy}:\n" +
                       $"* Ventas: ${totalMes:N0} ({tendencia} vs mes anterior: ${totalAnt:N0})\n" +
                       $"* Facturas: {(int)mes.FacturasMes} | Mes anterior: {(int)mesAnt.FacturasMes}";
            }

            case "detalle_factura":
            {
                var id = args["factura_id"]!.GetValue<int>();
                var cabecera = await con.QueryFirstOrDefaultAsync<dynamic>("""
                    SELECT f.FacturaID,
                           f.Fecha,
                           ISNULL(c.Nombre, 'Consumidor Final') AS Cliente,
                           ISNULL(c.NIT, '')                    AS NIT,
                           ISNULL(tot.Total, 0)                 AS Total,
                           f.TipDoc,
                           ISNULL(f.Notas, '')                  AS Notas
                    FROM Facturacion.Facturas f
                    LEFT JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
                    LEFT JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    WHERE f.FacturaID = @ID
                    """, new { ID = id });
                if (cabecera is null) return $"No existe factura #{id}.";

                var lineas = await con.QueryAsync<dynamic>("""
                    SELECT t.Referencia AS SKU,
                           t.Nombre    AS Articulo,
                           fl.Cantidad,
                           fl.PrecioUnitario,
                           ROUND(fl.Cantidad * fl.PrecioUnitario, 0) AS Subtotal
                    FROM Facturacion.FacturaLineas fl
                    JOIN Catalogo.Tarjetas t ON t.ArticuloID = fl.ArticuloID
                    WHERE fl.FacturaID = @ID
                    """, new { ID = id });

                var sb = new StringBuilder();
                sb.AppendLine($"**Factura #{cabecera.FacturaID}** - {((DateTime)cabecera.Fecha):dd/MM/yyyy} [{cabecera.TipDoc}]");
                sb.AppendLine($"Cliente: {cabecera.Cliente}{(string.IsNullOrEmpty((string)cabecera.NIT) ? "" : $" (NIT: {cabecera.NIT})")}");
                sb.AppendLine("---");
                foreach (var l in lineas)
                    sb.AppendLine($"* {l.Articulo} [{l.SKU}] x {(decimal)l.Cantidad:N2} @ ${(decimal)l.PrecioUnitario:N0} = **${(decimal)l.Subtotal:N0}**");
                sb.AppendLine("---");
                sb.AppendLine($"**Total: ${(decimal)cabecera.Total:N0}**");
                if (!string.IsNullOrWhiteSpace((string)cabecera.Notas))
                    sb.AppendLine($"Notas: {cabecera.Notas}");
                return sb.ToString();
            }

            case "facturas_cliente":
            {
                var cliente  = args["cliente"]!.GetValue<string>();
                var desdeStr = args["desde"]?.GetValue<string>();
                var hastaStr = args["hasta"]?.GetValue<string>();
                var desde    = string.IsNullOrWhiteSpace(desdeStr) ? (DateTime?)null : ParseFecha(desdeStr, "desde");
                var hasta    = string.IsNullOrWhiteSpace(hastaStr) ? (DateTime?)null : ParseFecha(hastaStr, "hasta");
                var limite   = args["limite"]?.GetValue<int>() ?? 20;

                var rows = await con.QueryAsync<dynamic>("""
                    SELECT TOP(@Limite)
                           f.FacturaID,
                           f.Fecha,
                           ISNULL(tot.Total, 0)                 AS Total,
                           f.TipDoc,
                           ISNULL(c.Nombre, 'Consumidor Final') AS Cliente
                    FROM Facturacion.Facturas f
                    LEFT JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
                    LEFT JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    WHERE (c.Nombre LIKE '%' + @Cliente + '%' OR c.NIT = @Cliente)
                      AND (@Desde IS NULL OR f.Fecha >= @Desde)
                      AND (@Hasta IS NULL OR f.Fecha <= @Hasta)
                    ORDER BY f.Fecha DESC, f.FacturaID DESC
                    """, new { Cliente = cliente, Desde = desde, Hasta = hasta, Limite = limite });

                var lista = rows.ToList();
                if (!lista.Any()) return $"No se encontraron facturas para '{cliente}'.";

                decimal totalAcumulado = lista.Sum(r => (decimal)r.Total);
                var sb = new StringBuilder($"Facturas de {lista[0].Cliente} ({lista.Count} encontradas):\n");
                foreach (var r in lista)
                    sb.AppendLine($"* #{r.FacturaID} - {((DateTime)r.Fecha):dd/MM/yyyy} - ${(decimal)r.Total:N0} [{r.TipDoc}]");
                sb.AppendLine($"**Total acumulado: ${totalAcumulado:N0}**");
                return sb.ToString();
            }

            case "clientes_top":
            {
                var desde  = ParseFecha(args["desde"]?.GetValue<string>(), "desde");
                var hasta  = ParseFecha(args["hasta"]?.GetValue<string>(), "hasta");
                var limite = args["limite"]?.GetValue<int>() ?? 10;
                var rows = await con.QueryAsync<dynamic>("""
                    SELECT TOP(@Limite)
                           ISNULL(c.Nombre, 'Consumidor Final') AS Cliente,
                           COUNT(*)        AS TotalFacturas,
                           SUM(tot.Total)  AS TotalComprado,
                           AVG(tot.Total)  AS TicketPromedio
                    FROM Facturacion.Facturas f
                    LEFT JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
                    JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    WHERE f.Fecha BETWEEN @Desde AND @Hasta
                    GROUP BY f.ClienteID, c.Nombre
                    ORDER BY TotalComprado DESC
                    """, new { Desde = desde, Hasta = hasta, Limite = limite });
                var lista = rows.ToList();
                if (!lista.Any()) return "Sin ventas en el periodo.";
                var sb = new StringBuilder($"Top {lista.Count} clientes ({desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}):\n");
                int i = 1;
                foreach (var r in lista)
                    sb.AppendLine($"{i++}. {r.Cliente} - ${(decimal)r.TotalComprado:N0} ({(int)r.TotalFacturas} facturas, ticket prom: ${(decimal)r.TicketPromedio:N0})");
                return sb.ToString();
            }

            case "buscar_articulo":
            {
                var busqueda = args["busqueda"]!.GetValue<string>();
                var rows = await con.QueryAsync<dynamic>("""
                    SELECT TOP(5)
                           t.Referencia,
                           t.Nombre,
                           t.PPublico,
                           t.PBodega,
                           ISNULL(t.CostoPromedio, 0)  AS Costo,
                           ISNULL(s.CantidadActual, 0) AS Stock,
                           t.StockMinimo,
                           CASE WHEN t.PPublico > 0
                                THEN (t.PPublico - ISNULL(t.CostoPromedio,0)) / t.PPublico * 100
                                ELSE 0 END AS MargenPct,
                           t.Estado
                    FROM Catalogo.Tarjetas t
                    LEFT JOIN Inventario.InventarioStock s ON s.ArticuloID = t.ArticuloID
                    WHERE t.Referencia = @B OR t.Nombre LIKE '%' + @B + '%'
                    ORDER BY CASE WHEN t.Referencia = @B THEN 0 ELSE 1 END
                    """, new { B = busqueda });
                var lista = rows.ToList();
                if (!lista.Any()) return $"No se encontro articulo con '{busqueda}'.";
                var sb = new StringBuilder();
                foreach (var r in lista)
                {
                    string stockAlerta = (decimal)r.Stock <= (decimal)r.StockMinimo ? " [STOCK CRITICO]" : "";
                    sb.AppendLine($"**{r.Nombre}** [{r.Referencia}] - Estado: {(r.Estado == true ? "Activo" : "Inactivo")}");
                    sb.AppendLine($"  * Precio publico: ${(decimal)r.PPublico:N0} | Bodega: ${(decimal)(r.PBodega ?? 0m):N0}");
                    sb.AppendLine($"  * Costo promedio: ${(decimal)r.Costo:N2} | Margen: {(decimal)r.MargenPct:N1}%");
                    sb.AppendLine($"  * Stock: {(decimal)r.Stock:N0} (min: {(decimal)r.StockMinimo:N0}){stockAlerta}");
                }
                return sb.ToString();
            }

            case "ventas_por_canal":
            {
                var desde = ParseFecha(args["desde"]?.GetValue<string>(), "desde");
                var hasta = ParseFecha(args["hasta"]?.GetValue<string>(), "hasta");
                var rows = await con.QueryAsync<dynamic>("""
                    SELECT f.TipDoc            AS Canal,
                           COUNT(*)            AS Facturas,
                           SUM(tot.Total)      AS TotalVentas,
                           AVG(tot.Total)      AS TicketPromedio
                    FROM Facturacion.Facturas f
                    JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    WHERE f.Fecha BETWEEN @Desde AND @Hasta
                    GROUP BY f.TipDoc
                    ORDER BY TotalVentas DESC
                    """, new { Desde = desde, Hasta = hasta });
                var lista = rows.ToList();
                if (!lista.Any()) return "Sin datos en el periodo.";
                decimal total = lista.Sum(r => (decimal)r.TotalVentas);
                var sb = new StringBuilder($"Ventas por tipo ({desde:dd/MM/yyyy} - {hasta:dd/MM/yyyy}):\n");
                foreach (var r in lista)
                {
                    decimal pct = total > 0 ? (decimal)r.TotalVentas / total * 100m : 0m;
                    sb.AppendLine($"* {r.Canal ?? "Sin tipo"}: ${(decimal)r.TotalVentas:N0} ({pct:N1}%) - {(int)r.Facturas} facturas - ticket: ${(decimal)r.TicketPromedio:N0}");
                }
                sb.AppendLine($"**Total: ${total:N0}**");
                return sb.ToString();
            }

            case "ordenes_produccion":
            {
                var soloActivas = args["solo_activas"]?.GetValue<bool>() ?? true;
                var limite      = args["limite"]?.GetValue<int>() ?? 10;
                var rows = await con.QueryAsync<dynamic>("""
                    SELECT TOP(@Limite)
                           o.CodigoOP,
                           t.Nombre               AS Producto,
                           o.CantidadProgramada,
                           o.CantidadProducidaReal,
                           e.Nombre               AS Estado,
                           o.FechaInicio,
                           o.FechaFin,
                           o.FechaPlanificada     AS FechaVencimiento
                    FROM Produccion.OrdenesProduccion o
                    JOIN Produccion.EstadosOP e ON e.EstadoOPID  = o.EstadoOPID
                    JOIN Catalogo.Tarjetas    t ON t.ArticuloID  = o.ProductoTerminadoID
                    WHERE (@SoloActivas = 0 OR e.Nombre IN ('Pendiente','En Proceso','Retrasada'))
                    ORDER BY o.FechaInicio DESC
                    """, new { Limite = limite, SoloActivas = soloActivas ? 1 : 0 });
                var lista = rows.ToList();
                if (!lista.Any()) return soloActivas ? "Sin ordenes de produccion activas." : "Sin ordenes registradas.";
                var sb = new StringBuilder(soloActivas ? "Ordenes de produccion activas:\n" : $"Ultimas {lista.Count} ordenes:\n");
                foreach (var r in lista)
                {
                    decimal progreso = (decimal)r.CantidadProgramada > 0
                        ? (decimal)r.CantidadProducidaReal / (decimal)r.CantidadProgramada * 100m : 0m;
                    string vence = r.FechaVencimiento is DateTime fv ? $" | vence {fv:dd/MM}" : "";
                    sb.AppendLine($"* {r.CodigoOP} - {r.Producto} - {r.Estado} - {progreso:N0}% ({(decimal)r.CantidadProducidaReal:N0}/{(decimal)r.CantidadProgramada:N0}){vence}");
                }
                return sb.ToString();
            }

            case "valor_inventario":
            {
                var rows = await con.QueryAsync<dynamic>("""
                    SELECT gm.Nombre                                                    AS Categoria,
                           COUNT(t.ArticuloID)                                         AS Articulos,
                           SUM(ISNULL(s.CantidadActual,0))                             AS StockTotal,
                           SUM(ISNULL(s.CantidadActual,0)*ISNULL(t.CostoPromedio,0))  AS ValorTotal
                    FROM Catalogo.Tarjetas t
                    LEFT JOIN Catalogo.GrupoMayor gm ON gm.GrupoMayorID = t.GrupoMayorID
                    LEFT JOIN Inventario.InventarioStock s ON s.ArticuloID = t.ArticuloID
                    WHERE t.Estado = 1
                    GROUP BY gm.GrupoMayorID, gm.Nombre
                    ORDER BY ValorTotal DESC
                    """);
                var lista = rows.ToList();
                decimal totalGeneral = lista.Sum(r => (decimal)r.ValorTotal);
                var sb = new StringBuilder("Valor del inventario:\n");
                foreach (var r in lista)
                    sb.AppendLine($"* {r.Categoria ?? "Sin categoria"}: {(int)r.Articulos} articulos - stock: {(decimal)r.StockTotal:N0} - valor: ${(decimal)r.ValorTotal:N0}");
                sb.AppendLine($"**Total valorizado: ${totalGeneral:N0}**");
                return sb.ToString();
            }

            case "comparar_periodos":
            {
                var p1d = ParseFecha(args["periodo1_desde"]?.GetValue<string>(), "periodo1_desde");
                var p1h = ParseFecha(args["periodo1_hasta"]?.GetValue<string>(), "periodo1_hasta");
                var p2d = ParseFecha(args["periodo2_desde"]?.GetValue<string>(), "periodo2_desde");
                var p2h = ParseFecha(args["periodo2_hasta"]?.GetValue<string>(), "periodo2_hasta");

                var q = """
                    SELECT ISNULL(SUM(tot.Total),0) AS Total, COUNT(*) AS Facturas, ISNULL(AVG(tot.Total),0) AS Ticket
                    FROM Facturacion.Facturas f
                    JOIN (
                        SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                        FROM Facturacion.FacturaLineas GROUP BY FacturaID
                    ) tot ON tot.FacturaID = f.FacturaID
                    WHERE f.Fecha BETWEEN @D AND @H
                    """;

                var p1 = await con.QueryFirstAsync<dynamic>(q, new { D = p1d, H = p1h });
                var p2 = await con.QueryFirstAsync<dynamic>(q, new { D = p2d, H = p2h });

                decimal t1 = (decimal)p1.Total, t2 = (decimal)p2.Total;
                decimal cambio = t2 > 0 ? (t1 - t2) / t2 * 100m : 0m;
                string tendencia = cambio >= 0 ? $"^ +{cambio:N1}%" : $"v {cambio:N1}%";

                return $"Comparacion de periodos:\n" +
                       $"**Periodo 1** ({p1d:dd/MM/yyyy} - {p1h:dd/MM/yyyy}): ${t1:N0} | {(int)p1.Facturas} facturas | ticket ${(decimal)p1.Ticket:N0}\n" +
                       $"**Periodo 2** ({p2d:dd/MM/yyyy} - {p2h:dd/MM/yyyy}): ${t2:N0} | {(int)p2.Facturas} facturas | ticket ${(decimal)p2.Ticket:N0}\n" +
                       $"**Variacion: {tendencia}** en ventas";
            }

            case "margen_productos":
            {
                var orden  = args["orden"]?.GetValue<string>() ?? "peor";
                var limite = args["limite"]?.GetValue<int>() ?? 10;
                bool mejorPrimero = orden == "mejor";
                var rows = await con.QueryAsync<dynamic>("""
                    SELECT TOP(@Limite)
                           t.Referencia AS SKU,
                           t.Nombre,
                           ISNULL(t.PPublico, 0)      AS PPublico,
                           ISNULL(t.CostoPromedio, 0) AS Costo,
                           CASE WHEN ISNULL(t.PPublico,0) > 0
                                THEN (ISNULL(t.PPublico,0) - ISNULL(t.CostoPromedio,0)) / t.PPublico * 100
                                ELSE 0 END AS MargenPct
                    FROM Catalogo.Tarjetas t
                    WHERE t.Estado = 1 AND ISNULL(t.PPublico,0) > 0
                    ORDER BY CASE WHEN @MejorPrimero = 1
                                  THEN -(ISNULL(t.PPublico,0) - ISNULL(t.CostoPromedio,0)) / NULLIF(t.PPublico,0) * 100
                                  ELSE  (ISNULL(t.PPublico,0) - ISNULL(t.CostoPromedio,0)) / NULLIF(t.PPublico,0) * 100
                             END ASC
                    """, new { Limite = limite, MejorPrimero = mejorPrimero ? 1 : 0 });
                var lista = rows.ToList();
                if (!lista.Any()) return "Sin articulos con precio configurado.";
                string titulo = orden == "mejor" ? $"Top {lista.Count} mejor margen:" : $"Top {lista.Count} peor margen:";
                var sb = new StringBuilder(titulo + "\n");
                int i = 1;
                foreach (var r in lista)
                    sb.AppendLine($"{i++}. {r.Nombre} [{r.SKU}] - precio: ${(decimal)r.PPublico:N0} | costo: ${(decimal)r.Costo:N2} | margen: **{(decimal)r.MargenPct:N1}%**");
                return sb.ToString();
            }

            default:
                return $"Tool '{nombre}' no reconocida.";
        }
    }

    // -- Chat principal ---------------------------------------------------------

    public async Task<ChatResponse> ChatAsync(ChatRequest req)
    {
        var apiKey = _cfg["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey no configurada.");
        var model  = _cfg["Groq:Model"]  ?? "openai/gpt-oss-120b";

        var empresa = await _config.ObtenerEmpresaAsync();

        var hoy = NexoApi.Common.ColombiaClock.Now;
        var sysPrompt = $"""
            Asistente analitico NEXO ERP · {empresa.NombreEmpresa} · {hoy:dd/MM/yyyy}.
            Idioma: espanol. Montos: $1.234.567. Nunca inventes cifras, usa tools.
            Analisis/recomendaciones: combina top_articulos+margen_productos y da conclusion accionable con 3-5 items concretos.
            Resumen negocio: kpis_mes+resumen_hoy. Stock critico: consultar_stock(solo_criticos=true).
            Si no hay tool para la pregunta, dilo brevemente. Respuestas cortas y directas.
            """;

        static JsonObject Msg(string role, string content) =>
            new() { ["role"] = role, ["content"] = content };

        var messages = new JsonArray();
        messages.Add(Msg("system", sysPrompt));
        // Limite de 6 mensajes anteriores (3 turnos) para controlar tokens
        var historialReciente = req.Historial.Count > 4
            ? req.Historial.TakeLast(4).ToList()
            : req.Historial;
        foreach (var m in historialReciente)
            messages.Add(Msg(m.Rol, m.Contenido));
        messages.Add(Msg("user", req.Mensaje));

        // Loop con tool calls (max 4 rondas)
        string respuestaFinal = "Lo siento, no pude procesar tu consulta.";

        for (int ronda = 0; ronda < 6; ronda++)
        {
            var body = new JsonObject
            {
                ["model"]       = model,
                ["messages"]    = messages.DeepClone(),
                ["tools"]       = _tools.DeepClone(),
                ["tool_choice"] = "auto",
                ["max_tokens"]  = 1500,
                ["temperature"] = 0.2
            };

            using var httpReq = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpReq.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            var resp = await _http.SendAsync(httpReq);

            var raw = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogError("Groq error {Status}: {Body}", resp.StatusCode, raw);
                respuestaFinal = (int)resp.StatusCode switch
                {
                    401 => "API key de Groq inválida o expirada. Verifica la configuración.",
                    429 => "Límite de solicitudes Groq alcanzado. Intenta en unos segundos.",
                    503 => "Servicio Groq no disponible temporalmente. Intenta de nuevo.",
                    _   => $"Error Groq {(int)resp.StatusCode}. Revisa los logs del servidor."
                };
                break;
            }

            var doc      = JsonDocument.Parse(raw);
            var choice   = doc.RootElement.GetProperty("choices")[0];
            var message  = choice.GetProperty("message");
            var finish   = choice.GetProperty("finish_reason").GetString();

            if (finish == "stop" || finish == "end_turn")
            {
                respuestaFinal = message.TryGetProperty("content", out var c) && c.ValueKind != JsonValueKind.Null
                    ? c.GetString() ?? respuestaFinal
                    : respuestaFinal;
                break;
            }

            if (finish == "tool_calls" && message.TryGetProperty("tool_calls", out var toolCalls))
            {
                messages.Add(JsonNode.Parse(message.GetRawText()));

                foreach (var tc in toolCalls.EnumerateArray())
                {
                    var tcId    = tc.GetProperty("id").GetString()!;
                    var fnName  = tc.GetProperty("function").GetProperty("name").GetString()!;
                    var fnArgs  = tc.GetProperty("function").GetProperty("arguments").GetString() ?? "{}";
                    var argsObj = JsonNode.Parse(fnArgs)?.AsObject() ?? new JsonObject();

                    string resultado;
                    try   { resultado = await EjecutarToolAsync(fnName, argsObj); }
                    catch (Exception ex) { resultado = $"Error al ejecutar {fnName}: {ex.Message}"; }

                    messages.Add(new JsonObject
                    {
                        ["role"]         = "tool",
                        ["tool_call_id"] = tcId,
                        ["content"]      = resultado
                    });
                }
                continue;
            }

            if (message.TryGetProperty("content", out var cont) && cont.ValueKind != JsonValueKind.Null)
                respuestaFinal = cont.GetString() ?? respuestaFinal;
            break;
        }

        var historialActualizado = req.Historial.ToList();
        historialActualizado.Add(new ChatMensaje("user",      req.Mensaje));
        historialActualizado.Add(new ChatMensaje("assistant", respuestaFinal));

        return new ChatResponse(respuestaFinal, historialActualizado);
    }
}
