using System.Security.Claims;
using Dapper;
using NexoApi.Common.Data;
using NexoApi.Features.Busqueda.Dtos;

namespace NexoApi.Features.Busqueda;

public interface IBusquedaService
{
    Task<List<ResultadoBusqueda>> BuscarAsync(string texto, ClaimsPrincipal usuario);
}

// Busqueda global de la barra superior. Cada categoria solo se consulta si el
// usuario tiene acceso a la pantalla de destino (mismos roles que el
// @attribute [Authorize] de la pagina Blazor correspondiente) -- si no, no
// tiene sentido sugerirle un resultado que al hacer click le va a negar el
// acceso.
public class BusquedaService : IBusquedaService
{
    private const int MaxPorCategoria = 5;
    private readonly IDbConnectionFactory _db;

    public BusquedaService(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<List<ResultadoBusqueda>> BuscarAsync(string texto, ClaimsPrincipal usuario)
    {
        var resultados = new List<ResultadoBusqueda>();
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 2)
            return resultados;

        var patron = $"%{texto.Trim()}%";
        using var connection = _db.CreateConnection();

        // Stock: sin restriccion de rol porque /inventario/stock es [Authorize]
        // sin roles especificos (todos los autenticados pueden verla) -- a
        // diferencia del catalogo de Articulos, que si esta restringido.
        {
            const string sqlStock = @"
                SELECT DISTINCT TOP (@Max) s.SKU, s.Articulo, s.Bodega
                FROM Inventario.vw_StockConsolidado s
                WHERE s.Articulo LIKE @Patron OR s.SKU LIKE @Patron
                ORDER BY s.Articulo";
            var stock = await connection.QueryAsync<(string SKU, string Articulo, string Bodega)>(

                sqlStock, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(stock.Select(s => new ResultadoBusqueda(
                "Stock", s.Articulo, $"SKU {s.SKU} · {s.Bodega}",
                "/inventario/stock")));
        }

        if (usuario.IsInRole("Administracion") || usuario.IsInRole("Empleados"))
        {
            const string sqlTraspasos = @"
                SELECT TOP (@Max) t.Codigo, bo.Nombre AS Origen, bd.Nombre AS Destino, t.EstadoTraspaso
                FROM Inventario.TraspasosBodega t
                JOIN Inventario.Bodegas bo ON bo.BodegaID = t.BodegaOrigenID
                JOIN Inventario.Bodegas bd ON bd.BodegaID = t.BodegaDestinoID
                WHERE t.Codigo LIKE @Patron 
                ORDER BY t.TraspasoID DESC";
            var traspasos = await connection.QueryAsync<(string Codigo, string Origen, string Destino, string EstadoTraspaso)>(
                sqlTraspasos, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(traspasos.Select(t => new ResultadoBusqueda(
                "Traspaso", t.Codigo, $"{t.Origen} → {t.Destino} · {t.EstadoTraspaso}",
                "/inventario/traspasos")));
        }

        if (usuario.IsInRole("Administracion") || usuario.IsInRole("Jefes"))
        {
            const string sqlArticulos = @"
                SELECT TOP (@Max) Referencia AS SKU, Nombre
                FROM Catalogo.Tarjetas
                WHERE Nombre LIKE @Patron OR Referencia LIKE @Patron
                ORDER BY Nombre";
            var articulos = await connection.QueryAsync<(string SKU, string Nombre)>(
                sqlArticulos, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(articulos.Select(a => new ResultadoBusqueda(
                "Artículo", a.Nombre, $"SKU {a.SKU} · Catálogo",
                $"/catalogo/articulos?texto={Uri.EscapeDataString(a.SKU)}")));
        }

        if (usuario.IsInRole("Administracion"))
        {
            // Usa ExternalId (no ClienteID) para que la URL del workspace no exponga el int primario.
            const string sqlClientes = @"
                SELECT TOP (@Max) ClienteID, ExternalId, Nombre, NIT
                FROM Crm.Clientes
                WHERE Nombre LIKE @Patron OR NIT LIKE @Patron
                ORDER BY Nombre";
            var clientes = await connection.QueryAsync<ClienteBusqueda>(sqlClientes, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(clientes.Select(c => new ResultadoBusqueda(
                "Cliente", c.Nombre, c.NIT is null ? "CRM" : $"NIT {c.NIT} · CRM",
                $"/crm/clientes/{c.ExternalId}")));

            const string sqlProveedores = @"
                SELECT TOP (@Max) RazonSocial, NIT
                FROM Catalogo.Proveedores
                WHERE RazonSocial LIKE @Patron OR NIT LIKE @Patron
                ORDER BY RazonSocial";
            var proveedores = await connection.QueryAsync<(string RazonSocial, string? NIT)>(
                sqlProveedores, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(proveedores.Select(p => new ResultadoBusqueda(
                "Proveedor", p.RazonSocial, p.NIT is null ? "Catálogo" : $"NIT {p.NIT} · Catálogo",
                "/catalogo/proveedores")));

            const string sqlCentrosCosto = @"
                SELECT TOP (@Max) Codigo, Nombre
                FROM Organizacion.CentrosCosto
                WHERE Nombre LIKE @Patron OR Codigo LIKE @Patron
                ORDER BY Nombre";
            var centrosCosto = await connection.QueryAsync<(string Codigo, string Nombre)>(
                sqlCentrosCosto, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(centrosCosto.Select(c => new ResultadoBusqueda(
                "Centro de Costo", c.Nombre, $"{c.Codigo} · Catálogo",
                "/catalogo/centros-costo")));

            const string sqlBodegas = @"
                SELECT TOP (@Max) b.Nombre, cc.Nombre AS CentroCosto
                FROM Inventario.Bodegas b
                JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = b.CentroCostoID
                WHERE b.Nombre LIKE @Patron
                ORDER BY b.Nombre";
            var bodegas = await connection.QueryAsync<(string Nombre, string CentroCosto)>(
                sqlBodegas, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(bodegas.Select(b => new ResultadoBusqueda(
                "Bodega", b.Nombre, $"{b.CentroCosto} · Catálogo",
                "/catalogo/bodegas")));
        }

        if (usuario.IsInRole("Administracion") || usuario.IsInRole("Jefes") || usuario.IsInRole("Subempleados"))
        {
            const string sqlOrdenesProduccion = @"
                SELECT TOP (@Max) op.CodigoOP, a.Nombre AS Producto, e.Nombre AS Estado
                FROM Produccion.OrdenesProduccion op
                JOIN Catalogo.Tarjetas a ON a.ArticuloID = op.ProductoTerminadoID
                JOIN Produccion.EstadosOP e ON e.EstadoOPID = op.EstadoOPID
                WHERE op.CodigoOP LIKE @Patron OR a.Nombre LIKE @Patron
                ORDER BY op.OrdenProduccionID DESC";
            var ordenesProduccion = await connection.QueryAsync<(string CodigoOP, string Producto, string Estado)>(
                sqlOrdenesProduccion, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(ordenesProduccion.Select(o => new ResultadoBusqueda(
                "Orden de Producción", o.CodigoOP, $"{o.Producto} · {o.Estado}",
                "/produccion/ordenes")));
        }

        if (usuario.IsInRole("Administracion") || usuario.IsInRole("Empleados"))
        {
            const string sqlOrdenesCompra = @"
                SELECT TOP (@Max) oc.Codigo, p.RazonSocial AS Proveedor, oc.EstadoOC AS Estado
                FROM Compras.OrdenesCompra oc
                JOIN Catalogo.Proveedores p ON p.ProveedorID = oc.ProveedorID
                WHERE oc.Codigo LIKE @Patron OR p.RazonSocial LIKE @Patron
                ORDER BY oc.OrdenCompraID DESC";
            var ordenesCompra = await connection.QueryAsync<(string Codigo, string Proveedor, string Estado)>(
                sqlOrdenesCompra, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(ordenesCompra.Select(o => new ResultadoBusqueda(
                "Orden de Compra", o.Codigo, $"{o.Proveedor} · {o.Estado}",
                "/compras/ordenes")));
        }

        // ---------- Modulos agregados agosto 2026 (antes quedaban fuera del buscador) ----------

        if (usuario.IsInRole("Administracion") || usuario.IsInRole("Jefes"))
        {
            const string sqlEmpleados = @"
                SELECT TOP (@Max) e.Nombres, e.Apellidos, c.Nombre AS Cargo
                FROM Rrhh.Empleados e
                LEFT JOIN Rrhh.Cargos c ON c.CargoID = e.CargoID
                WHERE e.Nombres LIKE @Patron OR e.Apellidos LIKE @Patron
                ORDER BY e.Nombres";
            var empleados = await connection.QueryAsync<EmpleadoBusqueda>(sqlEmpleados, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(empleados.Select(e => new ResultadoBusqueda(
                "Empleado", $"{e.Nombres} {e.Apellidos}", e.Cargo is null ? "RRHH" : $"{e.Cargo} · RRHH",
                "/rrhh/empleados")));
        }

        if (usuario.IsInRole("Administracion"))
        {
            const string sqlLeads = @"
                SELECT TOP (@Max) Nombre, Empresa, Etapa
                FROM Crm.Leads
                WHERE Nombre LIKE @Patron OR Empresa LIKE @Patron
                ORDER BY FechaCreacion DESC";
            var leads = await connection.QueryAsync<LeadBusqueda>(sqlLeads, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(leads.Select(l => new ResultadoBusqueda(
                "Lead", l.Nombre, l.Empresa is null ? $"{l.Etapa} · CRM" : $"{l.Empresa} · {l.Etapa} · CRM",
                "/crm/leads")));

            const string sqlOportunidades = @"
                SELECT TOP (@Max) o.Nombre, o.Etapa, ISNULL(c.Nombre, l.Nombre) AS Origen
                FROM Crm.Oportunidades o
                LEFT JOIN Crm.Clientes c ON c.ClienteID = o.ClienteID
                LEFT JOIN Crm.Leads l ON l.LeadID = o.LeadID
                WHERE o.Nombre LIKE @Patron
                ORDER BY o.FechaCreacion DESC";
            var oportunidades = await connection.QueryAsync<OportunidadBusqueda>(sqlOportunidades, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(oportunidades.Select(o => new ResultadoBusqueda(
                "Oportunidad", o.Nombre, $"{o.Origen} · {o.Etapa} · CRM",
                "/crm/oportunidades")));

            const string sqlFacturas = @"
                SELECT TOP (@Max) f.FacturaID, c.Nombre AS Cliente
                FROM Facturacion.Facturas f
                JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
                WHERE c.Nombre LIKE @Patron
                ORDER BY f.FacturaID DESC";
            var facturas = await connection.QueryAsync<FacturaBusqueda>(sqlFacturas, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(facturas.Select(f => new ResultadoBusqueda(
                "Factura", $"Factura #{f.FacturaID}", $"{f.Cliente} · Facturación",
                "/facturacion")));
        }

        if (usuario.IsInRole("Administracion") || usuario.IsInRole("Jefes"))
        {
            const string sqlProyectos = @"
                SELECT TOP (@Max) p.Nombre, p.Estado, c.Nombre AS Cliente
                FROM Proyectos.Proyectos p
                LEFT JOIN Crm.Clientes c ON c.ClienteID = p.ClienteID
                WHERE p.Nombre LIKE @Patron
                ORDER BY p.FechaInicio DESC";
            var proyectos = await connection.QueryAsync<ProyectoBusqueda>(sqlProyectos, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(proyectos.Select(p => new ResultadoBusqueda(
                "Proyecto", p.Nombre, p.Cliente is null ? p.Estado : $"{p.Cliente} · {p.Estado}",
                "/proyectos")));
        }

        if (usuario.IsInRole("Administracion") || usuario.IsInRole("Empleados"))
        {
            const string sqlDespachos = @"
                SELECT TOP (@Max) d.DespachoID, c.Nombre AS Cliente, d.Estado
                FROM Logistica.Despachos d
                JOIN Crm.Clientes c ON c.ClienteID = d.ClienteID
                WHERE c.Nombre LIKE @Patron
                ORDER BY d.DespachoID DESC";
            var despachos = await connection.QueryAsync<DespachoBusqueda>(sqlDespachos, new { Patron = patron, Max = MaxPorCategoria });
            resultados.AddRange(despachos.Select(d => new ResultadoBusqueda(
                "Despacho", $"GUIA-{d.DespachoID:D6}", $"{d.Cliente} · {d.Estado}",
                "/logistica/despachos")));
        }

        return resultados;
    }

    // Dapper no soporta mapear una fila directo a ValueTuple (leccion 20.30
    // en CLAUDE.md) -- los bloques nuevos usan records privados; los bloques
    // originales (arriba) siguen con ValueTuple sin tocar -- funcionan porque
    // QueryAsync de varias filas no dispara el bug (solo QuerySingleOrDefaultAsync
    // de una fila con tupla anulable), pero no repetir el patron en código nuevo.
    private record EmpleadoBusqueda(string Nombres, string Apellidos, string? Cargo);
    private record ClienteBusqueda(int ClienteID, string ExternalId, string Nombre, string? NIT);
    private record LeadBusqueda(string Nombre, string? Empresa, string Etapa);
    private record OportunidadBusqueda(string Nombre, string Etapa, string? Origen);
    private record FacturaBusqueda(int FacturaID, string Cliente);
    private record ProyectoBusqueda(string Nombre, string Estado, string? Cliente);
    private record DespachoBusqueda(int DespachoID, string Cliente, string Estado);
}
